using System;
using System.Linq;

namespace Gamesim.Simulation
{
    /// <summary>
    /// The All-Star player histories (ALL-STAR-HISTORIES-PLAN, slice H1b): in a fresh season that seats the real
    /// alumni, the house starts with what they did to and with each other on the show. Old partners start closer, a
    /// houseguest starts warier of whoever nominated them, and the Hitmen walk in with their final two. Everything after
    /// that is played as usual: nothing is forced, and an old partnership can split like any other.
    /// </summary>
    public sealed partial class EpisodeEngine
    {
        /// <summary>
        /// Writes a fresh season's alumni histories once, at alumni rules <see cref="AlumniRules.Histories"/>, from the
        /// edition new seasons start from (§6). <see cref="ShippedRules.ApplyFresh"/> calls it last, after agency's
        /// first impressions and the commitment rules, so a pair's standing is its first impression plus its history.
        ///
        /// <para><b>Only an unstarted season</b> (week one, revision 0, no command, alumni rules 0, no edition), and only
        /// one that seats at least one identity-intact, unique alumnus (<see cref="AlumniHistories.Seated"/>): any
        /// other season is left exactly as it is, and a season at 0 is never moved to 1 later. Never called by
        /// <see cref="SeasonBuilder.Create"/>, load, migration, recovery or the importer.</para>
        ///
        /// <para><b>The writers</b>, in fact-id order (§7):</para>
        /// <list type="bullet">
        /// <item>two houseguests: the shared standing through <see cref="RelationshipLedger.Move"/> (symmetric, no roll,
        /// no arc), then each directed part through <see cref="RelationshipLedger.RecordOneWay(EpisodeState, string, string, string, double, string, bool)"/>,
        /// permanent, on the holder's edge;</item>
        /// <item>the player on one of the cards: only the houseguest's receipt about the player. The player's own view is
        /// theirs (first impressions leave it alone for the same reason), so no <c>Move</c>, no <c>Change</c> and no
        /// arc;</item>
        /// <item>the Hitmen, both houseguests, under the commitment rules: one ordinary <c>final_two</c> through the
        /// deal pass's own writer (<see cref="NpcDeals.SeedOpeningPair"/>), with its usual fading <c>deal_accepted</c>.</item>
        /// </list>
        ///
        /// <para><b>No randomness</b>: no <c>Change</c>, roll, <c>StoryRandom</c> or draw on <see cref="EpisodeState.randomState"/>.
        /// The receipts and the deal take their sequence numbers from <see cref="EpisodeState.nextSequence"/> in this
        /// fixed order, so the same choice and seed always build the same house.</para>
        /// </summary>
        public static void EnableAlumniHistories(EpisodeState s)
        {
            if (s == null) throw new ArgumentNullException(nameof(s));
            bool unstarted = s.week == 1 && s.revision == 0 && s.acceptedCommandIds.Count == 0
                             && s.alumniRulesVersion == AlumniRules.Off && s.alumniCatalogEdition == null;
            if (!unstarted) return;
            var edition = AlumniEditions.Latest;
            if (AlumniHistories.Seated(s, edition).Count == 0) return;

            s.alumniRulesVersion = AlumniRules.Histories;
            s.alumniCatalogEdition = edition.Id;
            var pairs = AlumniHistories.SeatedPairs(s, edition);
            foreach (var pair in pairs)
            {
                bool houseguests = !s.Find(pair.FirstId).isPlayer && !s.Find(pair.SecondId).isPlayer;
                if (houseguests && pair.Fact.Standing != 0) RelationshipLedger.Move(s, pair.FirstId, pair.SecondId, pair.Fact.Standing);
                foreach (var part in pair.Fact.Parts)
                {
                    string holder = pair.On(part.HolderId), about = pair.On(part.AboutId);
                    if (s.Find(holder).isPlayer) continue;
                    RelationshipLedger.RecordOneWay(s, holder, about, AlumniHistories.ReceiptType, part.Impact, part.Description, permanent: true);
                }
            }
            if (!CommitmentRulesOn(s) || s.week < s.dealRulesStartWeek) return;
            foreach (var pair in pairs.Where(p => p.Fact.OpeningDeal == DealKind.FinalTwo))
                NpcDeals.SeedOpeningPair(s, pair.FirstId, pair.SecondId);
        }
    }
}
