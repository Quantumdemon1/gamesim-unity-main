using System;
using System.Collections.Generic;
using System.Linq;

namespace Gamesim.Simulation
{
    /// <summary>The friendship stages with a housemate (spec 3.8, slice S2): derived, never saved.</summary>
    public enum FriendStage
    {
        Stranger = 0,
        Acquainted = 1,
        Friendly = 2,
        Friend = 3,
        CloseFriend = 4,
        /// <summary>The existing loyalty oath (75): shown when the player has sworn one to them; it has no stage talk.</summary>
        RideOrDie = 5,
    }

    /// <summary>
    /// Friendship stages (slice S2, talk rules 2; spec 3.8 and the lead's rulings T3, T5 to T7). A stage is derived from the
    /// contact's rapport, their view of you and the week, through the chemistry's one gate path (<see cref="TalkChemistry.RapportGate"/>,
    /// <see cref="TalkChemistry.WeekGate"/>); it is never saved. "The first time a stage holds" is read from the stage talk's id among
    /// the contact's spent talks: while the highest stage that holds has an unspent stage talk, the lowest unspent stage talk up to
    /// it is the talk on offer, ahead of every other (a counted talk, under the same window cap), and playing it writes one event
    /// row. Pure, nothing drawn; every query is off (Stranger, false, null) below talk rules 2.
    /// </summary>
    public static class TalkFriendship
    {
        /// <summary>The event row a stage talk writes, in the form of the lore's line (its own kind).</summary>
        public const string StageEventKind = "talk-stage";

        /// <summary>The stage table's numbers (spec 3.8): rapport, their view of you, the week; a Guarded housemate's rapport gates +2.</summary>
        public const int AcquaintedRapport = 1, FriendlyRapport = 3, FriendRapport = 6, CloseRapport = 10;
        public const int FriendlyView = 15, FriendView = 35, CloseView = 60;
        public const int FriendWeek = 2, CloseWeek = 3;
        public const int GuardedRapport = 2;

        /// <summary>The favour (T6): +2 for "I've got you", -2 for "Can't promise that", one act each.</summary>
        public const int FavourWarmth = 2;

        /// <summary>A Friend's floor on telling the player the truth about their vote (T7).</summary>
        public const double FriendHonesty = 0.8;

        /// <summary>The further cost of nominating a Friend or closer, on their view of the player (T7).</summary>
        public const int BetrayalCost = -8;

        /// <summary>The stages' words, for the housemate's page.</summary>
        public static string Word(FriendStage stage)
        {
            switch (stage)
            {
                case FriendStage.Acquainted: return "Acquainted";
                case FriendStage.Friendly: return "Friendly";
                case FriendStage.Friend: return "Friend";
                case FriendStage.CloseFriend: return "Close friend";
                case FriendStage.RideOrDie: return "Ride or die";
                default: return "Stranger";
            }
        }

        /// <summary>The highest stage of the table (Stranger to Close friend) that holds with a housemate now; Stranger when slice 2 is off.</summary>
        public static FriendStage TableStage(EpisodeState s, string npcId)
        {
            if (!TalkChemistry.On(s)) return FriendStage.Stranger;
            var npc = s.Find(npcId);
            if (npc == null || npc.isPlayer) return FriendStage.Stranger;
            for (var stage = FriendStage.CloseFriend; stage >= FriendStage.Acquainted; stage--)
                if (Holds(s, npc, stage)) return stage;
            return FriendStage.Stranger;
        }

        /// <summary>The stage shown: <see cref="FriendStage.RideOrDie"/> when the player has sworn the oath to them, else the table's.</summary>
        public static FriendStage Stage(EpisodeState s, string npcId)
        {
            if (!TalkChemistry.On(s)) return FriendStage.Stranger;
            if (s.loyaltyOaths != null && s.loyaltyOaths.Any(o => o != null && o.playerId == s.playerId && o.targetId == npcId))
                return FriendStage.RideOrDie;
            return TableStage(s, npcId);
        }

        /// <summary>Whether a housemate is at <paramref name="stage"/> or closer (slice 2 on).</summary>
        public static bool AtLeast(EpisodeState s, string npcId, FriendStage stage) =>
            TalkChemistry.On(s) && Stage(s, npcId) >= stage;

        /// <summary>Whether one stage of the table holds with them: its rapport (Guarded +2, then the chemistry's shift), their view of you, its week (the chemistry's shift).</summary>
        public static bool Holds(EpisodeState s, ContestantState npc, FriendStage stage)
        {
            int rapport, view, week;
            switch (stage)
            {
                case FriendStage.Stranger: return true;
                case FriendStage.Acquainted: rapport = AcquaintedRapport; view = int.MinValue; week = 0; break;
                case FriendStage.Friendly: rapport = FriendlyRapport; view = FriendlyView; week = 0; break;
                case FriendStage.Friend: rapport = FriendRapport; view = FriendView; week = FriendWeek; break;
                case FriendStage.CloseFriend: rapport = CloseRapport; view = CloseView; week = CloseWeek; break;
                default: return false;
            }
            if (stage >= FriendStage.Friendly && FreeTalk.ClassOf(npc) == TalkPools.Guarded) rapport += GuardedRapport;
            int have = FreeTalk.Contact(s, npc.id)?.rapport ?? 0;
            if (have < TalkChemistry.RapportGate(s, npc.id, rapport)) return false;
            if (view != int.MinValue && s.Score(npc.id, s.playerId) < view) return false;
            return week <= 0 || s.week >= TalkChemistry.WeekGate(s, npc.id, week);
        }

        /// <summary>
        /// The stage talk on offer with a housemate, or null (T5): only at talk rules 2; while the highest table stage that holds
        /// has an unspent stage talk of theirs, the lowest stage up to it whose stage talk is unspent. A stage is spent when any
        /// stage talk of that stage is in the contact's spent talks. Among a stage's rows that hold, the talk's own keyed coin
        /// decides, as in <see cref="FreeTalk.Offer"/>. An All-Star's stage talks come only from the star pool. The cap and the
        /// refusals are <see cref="FreeTalk.Offer"/>'s; this reads no window.
        /// </summary>
        public static TalkRow DueTalk(EpisodeState s, string npcId)
        {
            if (!TalkChemistry.On(s)) return null;
            var npc = s.Find(npcId);
            if (npc == null || npc.isPlayer || npc.status != ContestantStatus.Active) return null;
            var top = TableStage(s, npcId);
            if (top < FriendStage.Acquainted) return null;
            var contact = FreeTalk.Contact(s, npcId);
            var spent = new HashSet<string>((contact?.spentTalks ?? new List<string>()).Select(TalkRules.SpentId), StringComparer.Ordinal);
            if (spent.Count >= TalkRules.MostSpentTalks) return null;
            var all = TalkBank.Rows.Where(t => t != null && t.Kind == TalkKind.Stage).ToList();
            bool StageSpent(FriendStage stage) => all.Any(t => t.Stage == stage && t.Id != null && spent.Contains(t.Id));
            var holding = all.Where(t => t.Stage >= FriendStage.Acquainted && t.Stage <= FriendStage.CloseFriend && t.Stage <= top
                && FreeTalk.Holds(s, npc, contact, t)).ToList();
            if (StageSpent(top) || !holding.Any(t => t.Stage == top)) return null;
            for (var stage = FriendStage.Acquainted; stage <= top; stage++)
            {
                if (StageSpent(stage)) continue;
                TalkRow best = null; double bestCoin = 2;
                foreach (var t in holding.Where(t => t.Stage == stage))
                {
                    double coin = StoryRandom.Unit(s, "talk:v1:q:" + npcId + ":" + t.Id);
                    if (coin < bestCoin) { best = t; bestCoin = coin; }
                }
                if (best != null) return best;
            }
            return null;
        }

        /// <summary>Whether a stage talk is the talk on offer with a housemate (slice 2 on, a window open and their talk not yet had).</summary>
        public static bool StageTalkDue(EpisodeState s, string npcId) =>
            FreeTalk.Offer(s, npcId) is TalkOffer offer && offer.Available && offer.Talk.Kind == TalkKind.Stage;

        /// <summary>The event row's line when a stage talk is played: "{name} counts you as a friend now." and its kin.</summary>
        public static string EventLine(FriendStage stage, string name)
        {
            switch (stage)
            {
                case FriendStage.Acquainted: return "You and " + name + " know each other now.";
                case FriendStage.Friendly: return "You and " + name + " are friendly now.";
                case FriendStage.Friend: return name + " counts you as a friend now.";
                case FriendStage.CloseFriend: return name + " counts you as a close friend now.";
                default: return "";
            }
        }

        /// <summary>
        /// The housemates the player is about to put on the block who are at Friend or closer, read before the nomination's own
        /// effects (T7): empty unless slice 2 is on and the player is the Head of Household naming them.
        /// </summary>
        public static List<string> FriendsNamed(EpisodeState s, IEnumerable<string> nomineeIds)
        {
            var friends = new List<string>();
            if (!TalkChemistry.On(s) || s.hohId == null || s.hohId != s.playerId || nomineeIds == null) return friends;
            foreach (var id in nomineeIds)
                if (id != null && id != s.playerId && !friends.Contains(id) && AtLeast(s, id, FriendStage.Friend)) friends.Add(id);
            return friends;
        }
    }
}
