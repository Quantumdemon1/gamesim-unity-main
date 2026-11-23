using System.Linq;

namespace Gamesim.Simulation
{
    public sealed partial class EpisodeEngine
    {
        /// <summary>
        /// Dialogue rules 4's nominee grant (DIALOGUE-PLAN §4, slice D5a), written when the player is nominated: at the week's
        /// nominations as they commit, or by a replacement nomination after the veto. One a week: a week that already holds
        /// the player's grant gets no second, so being renominated writes nothing. The grant records the nomination that
        /// wrote it and draws nothing. Nothing in a season below rules 4.
        /// </summary>
        internal static void GrantTheNominee(EpisodeState s, string sourceKind)
        {
            if (!DialogueRules.GrantsOn(s) || s.nominees == null || !s.nominees.Contains(s.playerId)
                || s.Find(s.playerId)?.status != ContestantStatus.Active) return;
            string id = DialogueRules.GrantId(s.week, DialogueRules.NomineeRole);
            if (s.dialogue.grants.Any(g => g != null && g.id == id)) return;
            s.dialogue.grants.Add(new DialogueRoleGrantState
            {
                id = id, ownerId = s.playerId, week = s.week, role = DialogueRules.NomineeRole, sourceKind = sourceKind,
                status = DialogueRules.GrantOpen, sourceRevision = s.revision, sourceSequence = s.nextSequence - 1,
                sourcePhase = s.phase, sourceNominees = s.nominees.ToList(),
            });
        }

        /// <summary>
        /// Dialogue rules 4's steps after the house's veto decision: a replacement nomination of the player grants their
        /// conversation (D5a), then the conditional promises settle in their own step (D4). An appeal to the holder left open
        /// lapsed before the decision, inside the pending command that owns it; the house's own rows written here (the
        /// occasion's decision) join that command's expected delta, so the decision commits.
        /// </summary>
        internal static void AfterTheVetoDecision(EpisodeState s, bool used, string saved, string replacement, PendingDialogueCommand pending)
        {
            if (used && replacement == s.playerId) GrantTheNominee(s, DialogueRules.FromReplacement);
            SettleConditionalVeto(s, used, saved, replacement);
            if (pending?.SealedFor(s) != null) pending.AdmitHouseRows(s);
        }

        internal sealed partial class PendingDialogueCommand
        {
            // The entry a grant pays (D5a): no window tick and no action, its grant named.
            private DialogueEntryReceiptState GrantEntry(DialogueRoleGrantState grant, int before) => new DialogueEntryReceiptState
                { kind = DialogueRules.GrantEntry, grantId = grant.id, revision = revision, windowActionBefore = before, windowActionAfter = before };

            /// <summary>The grant is used up by the conversation's first accepted node, in the candidate and in the expected delta alike.</summary>
            private void UseGrant(EpisodeState state, string grantId, string episodeId)
            {
                var row = state.dialogue.grants.Single(g => g != null && g.id == grantId);
                var mirror = expected.grants.Single(g => g != null && g.id == grantId);
                Require(row.status == DialogueRules.GrantOpen && mirror.status == DialogueRules.GrantOpen, "That conversation on the house was already used.");
                foreach (var g in new[] { row, mirror })
                {
                    g.status = DialogueRules.GrantUsed; g.consumedEpisodeId = episodeId; g.consumedRevision = revision;
                }
            }

            /// <summary>
            /// The house's own dialogue rows (veto occasions and grants) as the command left them, admitted to the expected
            /// delta after the conversation's lapse was sealed: they are the house's, never the conversation's, and the
            /// whole-state validator judges them. Only for a command whose conversation has lapsed and been sealed.
            /// </summary>
            internal void AdmitHouseRows(EpisodeState state)
            {
                Require(Bound(state) && sealedDelta && Same(source, original), "The house's rows are not this command's to admit.");
                expected.vetoOccasions = state.dialogue.vetoOccasions?.Select(o => o?.Clone()).ToList();
                expected.grants = state.dialogue.grants?.Select(g => g?.Clone()).ToList();
            }
        }
    }
}
