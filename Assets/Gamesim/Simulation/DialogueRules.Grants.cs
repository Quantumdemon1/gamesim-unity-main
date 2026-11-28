using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Gamesim.Simulation
{
    /// <summary>
    /// Dialogue rules 4's nominee grant (DIALOGUE-PLAN §4, slice D5a): when the player is nominated, originally or as the
    /// replacement, the week gives them one conversation on the house. The grant pays for one newly started conversation in
    /// place of the window's action - a campaign conversation or the appeal to the veto holder, never the veto question
    /// (decision 10) - and is used up by that conversation's first accepted node, staying used if the conversation lapses or
    /// is left. One grant a role a week, at most <see cref="FreeConversationsAWeek"/> free conversations a week whatever
    /// their source, never carried into another week: an open grant of an earlier week pays for nothing. Counting is on the
    /// retained rows (§5.5): no weekly-cap field.
    ///
    /// <para>Every row has its home at schema 30 (<see cref="DialogueRoleGrantState"/>; §6): no saved field is added. Below
    /// rules 4 nothing here writes, reads differently or draws, and the store keeps refusing every grant row. Nothing is
    /// drawn from the season's stream.</para>
    /// </summary>
    public static partial class DialogueRules
    {
        /// <summary>The one role that grants a conversation at rules 4: the nominee (§4's table). The consultations' are rules 5's (<see cref="HoHRole"/>).</summary>
        public const string NomineeRole = "nominee";

        /// <summary>What wrote a nominee grant: the week's original nominations, or a replacement nomination after the veto.</summary>
        public const string FromNominations = "nominations", FromReplacement = "replacement";

        /// <summary>A grant's status: open until a conversation's entry uses it, then used for good.</summary>
        public const string GrantOpen = "open", GrantUsed = "used";

        /// <summary>The free conversations a player may have in one week, however their roles overlap (§4).</summary>
        public const int FreeConversationsAWeek = 2;

        /// <summary>The one plain line the person's menu and the conversation's entry show while a grant would pay.</summary>
        public const string GrantLine = "One conversation this week is on the house.";

        /// <summary>Whether a season's dialogue rules write and spend role grants: rules 4 (<see cref="GrantFundingFrom"/>) and later.</summary>
        public static bool GrantsOn(EpisodeState s) => On(s) && s.dialogueRulesVersion >= GrantFundingFrom;

        /// <summary>A grant's id: one a role a week, so a renomination or a reload can never write a second.</summary>
        public static string GrantId(int week, string role) =>
            "dialogue-grant-" + week.ToString(CultureInfo.InvariantCulture) + "-" + role;

        /// <summary>The conversations a grant paid for in this week: the count every weekly bound reads.</summary>
        public static int FreeConversationsUsed(EpisodeState s, int week) =>
            s?.dialogue?.episodes?.Count(e => e != null && e.week == week && e.entry?.kind == GrantEntry) ?? 0;

        /// <summary>
        /// The grant that pays for a conversation of this kind started now, or null: rules 4 and later, never the veto
        /// question, this week's open grant of the player, and fewer than two free conversations had this week. From rules 5
        /// a grant pays only for its own role's conversations (<see cref="GrantRoleFor"/>): the HoH's for the HoH consultation,
        /// the nominee's for the others; below rules 5 the nominee's is the only grant there is. Reads only.
        /// </summary>
        internal static DialogueRoleGrantState GrantFor(EpisodeState s, string kind)
        {
            if (!GrantsOn(s) || kind == DialogueVetoQuestion.Kind || s.dialogue?.grants == null
                || FreeConversationsUsed(s, s.week) >= FreeConversationsAWeek) return null;
            return s.dialogue.grants.FirstOrDefault(g => g != null && g.week == s.week && g.ownerId == s.playerId && g.status == GrantOpen
                && (s.dialogueRulesVersion < Rules5 || g.role == GrantRoleFor(kind)));
        }

        /// <summary>Whether a conversation of the kind the phase holds, started now, would be paid by a grant. Reads only.</summary>
        public static bool GrantPaysNow(EpisodeState s) => GrantFor(s, KindNow(s)) != null;

        /// <summary>Whether a conversation with this houseguest can start now and would be on the house: the HUD's one question.</summary>
        public static bool OnTheHouseWith(EpisodeState s, string npcId) => GrantPaysNow(s) && CanStart(s, npcId, out _);

        // ---------------------------------------------------------------- the validator

        /// <summary>
        /// The grants a rules-4 season holds: each exactly the row its nomination wrote, one a role a week in week order, open
        /// with nothing spent or used by exactly the one conversation it paid for, and no week with more than two free
        /// conversations.
        /// </summary>
        private static bool GrantsRules4(EpisodeState s, out string error)
        {
            error = null;
            var rows = s.dialogue.grants;
            if (rows.Any(g => g == null) || rows.GroupBy(g => g.id).Any(g => g.Count() != 1))
                return Fail(out error, "Role grants must be unique.");
            if (s.dialogue.episodes.Where(e => e?.entry?.kind == GrantEntry).GroupBy(e => e.week).Any(g => g.Count() > FreeConversationsAWeek))
                return Fail(out error, "A week has at most two free conversations.");
            int lastWeek = 0;
            foreach (var g in rows)
            {
                // From rules 5 the Head of Household's grant (DialogueRules.Rules5.cs), checked on its own terms; the nominee's below.
                if (HoHGrantSource(s, g))
                {
                    if (g.id != GrantId(g.week, g.role) || g.ownerId != s.playerId || g.week < 1 || g.week > s.week || g.week < lastWeek
                        || g.sourceRevision < 0 || g.sourceRevision > s.revision || g.sourceSequence < 1 || g.sourceSequence >= s.nextSequence
                        || g.expiryReason != null || g.expiredRevision != 0)
                        return Fail(out error, "A role grant must keep the crowning that wrote it.");
                }
                else
                {
                    bool source = g.sourceKind == FromNominations ? g.sourcePhase == EpisodePhase.Nomination
                        : g.sourceKind == FromReplacement && g.sourcePhase == EpisodePhase.VetoMeeting;
                    if (g.role != NomineeRole || g.id != GrantId(g.week, g.role) || g.ownerId != s.playerId
                        || g.week < 1 || g.week > s.week || g.week < lastWeek || !source
                        || g.sourceRevision < 0 || g.sourceRevision > s.revision || g.sourceSequence < 1 || g.sourceSequence >= s.nextSequence
                        || g.sourceNominees == null || !g.sourceNominees.Contains(s.playerId)
                        || g.sourceNominees.Any(id => string.IsNullOrEmpty(id) || s.Find(id) == null)
                        || g.sourceNominees.Distinct().Count() != g.sourceNominees.Count
                        || g.expiryReason != null || g.expiredRevision != 0)
                        return Fail(out error, "A role grant must keep the nomination that wrote it.");
                }
                lastWeek = g.week;
                var users = s.dialogue.episodes.Where(e => e?.entry?.kind == GrantEntry && e.entry.grantId == g.id).Take(2).ToArray();
                if (g.status == GrantOpen ? users.Length != 0 || g.consumedEpisodeId != null || g.consumedRevision != 0
                    : g.status != GrantUsed || users.Length != 1 || g.consumedEpisodeId != users[0].id
                      || g.consumedRevision != users[0].entry.revision)
                    return Fail(out error, "A role grant must be used by exactly the conversation it paid for.");
            }
            return true;
        }

        /// <summary>
        /// A conversation's entry paid by a grant (rules 4 and later): it spends no window tick and moves no action count, it
        /// names a grant of its own week written before it started, and no other entry spends that grant.
        /// </summary>
        private static bool GrantEntryRules4(EpisodeState s, DialogueEpisodeState e, EpisodeEngine.PendingDialogueCommand pending,
            HashSet<string> ticks, out string error)
        {
            error = null;
            string identity = EntryGrant(e.entry);
            var grant = identity == null ? null : s.dialogue.grants.FirstOrDefault(g => g != null && g.id == e.entry.grantId);
            if (!GrantsOn(s) || grant == null || grant.week != e.week || grant.sourceSequence >= e.startedSequence
                // From rules 5 a grant pays only for its own role's conversations; below it the nominee's is the only grant.
                || s.dialogueRulesVersion >= Rules5 && grant.role != GrantRoleFor(e.occasionKind)
                || e.entry.revision != e.startedRevision || !Revision(s, e.entry.revision, e.entry, DialogueRevisionField.Entry, pending)
                || e.entry.windowActionBefore < 0 || e.entry.windowActionAfter > MaximumAction
                || e.entry.windowActionAfter != e.entry.windowActionBefore
                || e.week == s.week && e.entry.windowActionAfter > s.windowActions[e.window]
                || !ticks.Add(identity))
                return Fail(out error, "Invalid granted dialogue entry receipt.");
            return true;
        }
    }
}
