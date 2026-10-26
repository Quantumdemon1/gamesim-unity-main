using System;
using System.Collections.Generic;
using System.Linq;

namespace Gamesim.Simulation
{
    /// <summary>
    /// The alumni histories' receipts (ALL-STAR-HISTORIES-PLAN §5.3, slice H1b). The rules value and its edition are
    /// checked with the other pooled fields; this checks what the histories wrote into the ledger. At alumni rules 0 a
    /// ledger entry's type is free text, as it was at schema 29, so an <see cref="AlumniHistories.ReceiptType"/> entry
    /// there is an ordinary entry and nothing here refuses it.
    /// </summary>
    public static partial class EpisodeValidation
    {
        private static bool TryValidateAlumni(EpisodeState s, out string error)
        {
            error = null;
            var edition = AlumniHistories.EditionOf(s);
            if (edition == null) return true;
            // Houseguest id to the template the edition finds them on: intact and unique only (§3.5).
            var templateOf = AlumniHistories.Seated(s, edition).ToDictionary(kv => kv.Value, kv => kv.Key, StringComparer.Ordinal);
            foreach (var edge in s.relationships)
            {
                List<RelationshipEventState> receipts = null;
                foreach (var entry in edge.events)
                    if (AlumniHistories.IsReceipt(entry)) (receipts ??= new List<RelationshipEventState>()).Add(entry);
                if (receipts == null) continue;
                // Alone of its type on its edge; held by a houseguest, never the player; on an edition pair the house
                // seats, in a direction the edition gives a part; week one, permanent, and the part's own words and weight.
                var holder = s.Find(edge.fromId);
                AlumniPriorPart part = null;
                if (receipts.Count == 1 && holder != null && !holder.isPlayer
                    && templateOf.TryGetValue(edge.fromId, out string holderTemplate) && templateOf.TryGetValue(edge.toId, out string aboutTemplate))
                    part = edition.PartFor(holderTemplate, aboutTemplate);
                var receipt = receipts[0];
                if (part == null || receipt.week != 1 || receipt.decayable || receipt.impactScore != part.Impact
                    || !string.Equals(receipt.description, part.Description, StringComparison.Ordinal))
                    return Fail(out error, "Invalid alumni history receipt.");
            }
            return true;
        }
    }
}
