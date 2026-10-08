using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Gamesim.Simulation
{
    /// <summary>
    /// The commitment rules (ACTIONS-DEALS-ALLIANCES-PLAN R0 and C0, schema 22): the one boundary
    /// every rule of that plan shares. Under them a study is one of the window's actions (X1), a
    /// pact-mate's word about a threat reaches the player as a line and leaves their view of the
    /// threat alone (X6), a whisper reaches the person the player is talking to (X7), a houseguest's
    /// rumour to the player says what was said and moves only the player's own view of its subject
    /// (X8), talk about the player that the player never heard moves only the listener's view of
    /// them, a promise to vote somebody out is never made to them, and a deal or a promise records
    /// who broke it and when (C0) - so every reader, the eviction vote's included, holds a breach
    /// against whoever broke it (<see cref="Breaches"/>), and the record of it never fades (X11).
    ///
    /// <para>And every deal does something (C1): an information deal passes the player one reading a
    /// week (<see cref="PassTheReadings"/>), a partnership is judged at the vote and a safety pact is
    /// kept by being spared (<see cref="DealResolution"/>), a final two deal is an obligation in the
    /// final Head of Household's choice (<see cref="FinalTwoTerms"/>), deals and final two promises
    /// end with an evictee and the final choice passes over anybody gone (X4,
    /// <see cref="EndWithTheEvictee"/>), a vote deal both parties broke names neither, and accepting
    /// an offer is +4 and commits the player: broken, it weighs one step heavier (decision 15).</para>
    ///
    /// <para>Keyed to <see cref="EpisodeState.commitmentRulesStartWeek"/>: before it a season plays
    /// exactly as it did, roll for roll and line for line. The director starts every season under
    /// them, the importer from the week after the import; a season saved before they existed keeps
    /// playing without them to its end, and so does every season a test builds directly.</para>
    /// </summary>
    public sealed partial class EpisodeEngine
    {
        /// <summary>Switches the commitment rules on from a week, no later than the week after the season's own.</summary>
        public static void EnableCommitments(EpisodeState s, int fromWeek = 1)
        {
            if (s == null) throw new ArgumentNullException(nameof(s));
            s.commitmentRulesStartWeek = Math.Max(1, Math.Min(fromWeek, s.week + 1));
        }

        /// <summary>Whether the season plays under the commitment rules this week.</summary>
        public static bool CommitmentRulesOn(EpisodeState s) =>
            s != null && s.commitmentRulesStartWeek >= 1 && s.week >= s.commitmentRulesStartWeek;

        /// <summary>
        /// What a houseguest told the player about somebody, under the commitment rules (X8): the
        /// line says what was said, the player remembers who said it, and only the player's own view
        /// of the one it was about moves - by the rumour's weight, one way, on the player's own
        /// record. Nothing moves in the view of the one it was about, who heard nothing. No roll.
        ///
        /// <para>Except toward somebody the player is in a pact with: their view of a pact-mate is
        /// theirs to change, and a rumour's weight could take it under the line a pact sours at, so
        /// the pact would end without a word (<see cref="NpcAlliances.Dissolve"/>). The line and the
        /// memory say it was said; the record holds it at nothing, and the view does not move.</para>
        /// </summary>
        internal static void HeardFrom(EpisodeState s, string tellerId, string subjectId, double delta, string said)
        {
            var teller = s.Find(tellerId);
            var subject = s.Find(subjectId);
            if (teller == null || subject == null || subject.isPlayer) return;
            string line = teller.name + " told you " + said + ".";
            bool pactMate = s.Allied(s.playerId, subjectId);
            if (!pactMate) WriteScore(s, s.playerId, subjectId, delta);
            RelationshipLedger.RecordOneWay(s, s.playerId, subjectId, "rumor", pactMate ? 0 : delta, line);
            Remember(s, s.playerId, subjectId, teller.name + " told me in week " + s.week + " that " + said + ".", true);
            Log(s, "information", line, s.playerId);
        }

        /// <summary>
        /// Talk about the player that the player never heard, under the commitment rules: a rumour
        /// about them, or a hunt with them as its threat. The listener's view of the player moves as
        /// the engine's own path moves it - weighed by the listener's social stat, on the listener's
        /// record, into the arc the two of them share - and the player's view of the listener does
        /// not: the player was not in the room. The two-way path also wrote the player's half, with a
        /// roll; this draws none.
        /// </summary>
        internal static void HeardAbout(EpisodeState s, string listenerId, double delta, string note, string type)
        {
            var listener = s.Find(listenerId);
            if (listener == null || listener.isPlayer) return;
            double adjusted = WebRules.RelationshipDelta(delta, listener.stats.social, false);
            string reason = note ?? "Relationship changed by " + adjusted.ToString(CultureInfo.InvariantCulture);
            if (type != null) AddRelationshipEvent(s, listenerId, s.playerId, adjusted, reason, type);
            WriteScore(s, listenerId, s.playerId, adjusted);
            var relation = s.relationships.Single(r => r.fromId == listenerId && r.toId == s.playerId);
            relation.lastInteractionWeek = s.week;
            if (note != null) { relation.notes.Add(note); if (relation.notes.Count > 256) relation.notes.RemoveAt(0); }
            Arc(s, listenerId, adjusted, reason);
        }

        /// <summary>
        /// A deal broken under the commitment rules, on the record (C0, X11): the one wronged holds it
        /// against the one who broke it, permanently, at the deal's weight; the one who broke it keeps
        /// a record that it happened, weighing nothing, so their own record still says what they did
        /// (<see cref="YourWeek"/> reads the player's) without the victim reading as untrusted. The
        /// same two entries, in the same order, as the two-way record the season used before.
        /// </summary>
        private static void RecordBreach(EpisodeState s, string wrongedId, string breakerId, double delta, string text)
        {
            RelationshipLedger.RecordOneWay(s, wrongedId, breakerId, "deal_broken", delta, text, permanent: true);
            RelationshipLedger.RecordOneWay(s, breakerId, wrongedId, "deal_broken", 0, text, permanent: true);
        }

        // ---------------------------------------------------------------- C1: every deal does something

        /// <summary>
        /// What a partnership kept at a vote leaves on the record, both ways: a kept deal's entry at a
        /// word's weight (+4, a conversation's), so a partnership kept at every vote that tests it is
        /// never a farm of standing. A record, which moves no score.
        /// </summary>
        public const double PartnershipKept = 4;

        /// <summary>
        /// A partnership kept at a vote, under the commitment rules (C1): it stands - no status, no
        /// settlement, no week on its record - and the keep is a small kept record: the entry
        /// (<see cref="PartnershipKept"/>), the memory of the one kept, and the line, which goes to
        /// the voter alone, since the ballot that kept it is theirs. Their memory and the record wait
        /// for the player who was kept to know that ballot, as a vote deal's do
        /// (<see cref="KnownBallots.TellsAnUnknownBallot"/>). Nothing is drawn and no view moves.
        /// </summary>
        private static void KeptAndStanding(EpisodeState s, DealResolution.Verdict verdict)
        {
            var deal = verdict.deal;
            string kept = DealResolution.Partner(deal, verdict.actorId);
            if (kept == null) return;
            string text = Name(s, verdict.actorId) + " honoured a " + DealKind.Title(deal.type).ToLowerInvariant() + " with " + Name(s, kept) + ".";
            RelationshipLedger.Record(s, kept, verdict.actorId, "deal_fulfilled", PartnershipKept, text);
            Remember(s, kept, verdict.actorId, text, true);
            Log(s, "deal-outcome", text, verdict.actorId);
        }

        /// <summary>
        /// Somebody has left the house, under the commitment rules (ACTIONS-DEALS-ALLIANCES-PLAN C1,
        /// X4): every deal that still binds them - one they are a party to, an offer they made or were
        /// made, one that names them - ends, and so does every final two promise made to them or by
        /// them. An ending is not a breach, as a deal whose week has passed is not
        /// (<see cref="NpcDeals"/>' expiry): nobody failed anybody when a partner was voted out, so
        /// nothing is written to anybody's record, nothing is logged and nothing is drawn. Before the
        /// rules they stood, and the final eviction broke every final two still standing with somebody
        /// already on the jury - a deal at −45 and a grudge, −50 with the jury - for a word nobody could
        /// keep.
        /// </summary>
        private static void EndWithTheEvictee(EpisodeState s, string evictedId)
        {
            if (string.IsNullOrEmpty(evictedId)) return;
            ResolveUnifiedVoteExpiry(s, UnifiedCommitmentExpiry.Departure, evictedId);
            ResolveUnifiedSafetyExpiry(s, UnifiedCommitmentExpiry.Departure, evictedId);
            foreach (var deal in s.deals.Where(d => DealStatus.Binds(d.status)
                         && (d.proposerId == evictedId || d.recipientId == evictedId || d.targetId == evictedId)))
                deal.status = DealStatus.Expired;
            foreach (var promise in s.promises.Where(p => p.status == PromiseStatus.Active && p.kind == PromiseKind.FinalTwo
                         && (p.fromId == evictedId || p.toId == evictedId)))
                promise.status = PromiseStatus.Expired;
        }

        /// <summary>
        /// Whether the final choice settles this promise: a final two promise the final Head of
        /// Household made that still stands - and, under the commitment rules (C1, X4), made to
        /// somebody still in the house. One to a juror ended as they left; one that did not (a season
        /// that took the rules on later) is passed over, not broken. The engine's final eviction and
        /// the decision screen's dry run (<see cref="CommitmentsRead"/>) both ask this.
        /// </summary>
        public static bool FinalChoiceSettles(EpisodeState s, PromiseState p, string hohId) =>
            p != null && p.status == PromiseStatus.Active && p.kind == PromiseKind.FinalTwo && p.fromId == hohId
            && (!CommitmentRulesOn(s) || s.Find(p.toId)?.status == ContestantStatus.Active);

        /// <summary>
        /// What a final two deal weighs in the final Head of Household's choice under the commitment
        /// rules, at its whole: the evaluator's own decisive margin (a lean of 25 is "decisive"), so a
        /// formally active final two outweighs any lean short of a decisive one, before their word multiplier.
        /// </summary>
        public const double FinalTwoObligation = 25;

        /// <summary>
        /// A final two deal in the final Head of Household's choice (C1): an obligation term on the
        /// finalist it would take, as a vote deal is a term in a ballot (<see cref="Obligations"/>),
        /// at fixed strength while formally active, scaled by their word (Loyal ×1.5, Sneaky ×0),
        /// and by the existing hold multiplier for a called-in promise. Current liking is a separate
        /// evaluator factor. Overlapping commitments use the strongest and retain every supporting
        /// record through <see cref="EndgameCommitments"/>. A final two deal used
        /// to be the web's deal term alone, about 4.5 points after its weight, which decided nothing.
        /// Only a deal with one of the two finalists: one with a juror is no longer a choice. Empty for
        /// a Head of Household who is the player, whose choice is their own. No roll.
        /// </summary>
        public static List<WebVoteObligation> FinalTwoTerms(EpisodeState s, string hohId, IReadOnlyList<string> finalists)
            => EndgameCommitments.Read(s, hohId, finalists).Where(commitment => commitment.HasFinalTwo)
                .Select(commitment => commitment.ToObligation()).ToList();

        /// <summary>
        /// An information deal does something (C1): each week, as campaigning closes and the house
        /// is about to vote, a partner of the player's who casts a ballot this week tells them where
        /// it is going. It is the same claim an answered "where's your head at" is - told to the
        /// player's face (<see cref="ClaimSource.Told"/>), on the ledger's claims, read by the whip
        /// count, and judged at the reveal like every other. Their word is as good as it is to anybody
        /// who asks (<see cref="VoteHonesty"/>: Loyal always, Sneaky one time in four, anybody else by
        /// how they see the player; and, since C6, an ally always: <see cref="AnswerHonesty"/>), drawn
        /// on a coin keyed to the week and the partner, so the season's stream is untouched; a lie
        /// names the nominee they are not voting out. The deal obliges an answer, so nobody
        /// deflects. A partner caught lying at the reveal has broken the deal
        /// (<see cref="BreakTheDealsOfLyingPartners"/>). A partner who casts no ballot this week - the
        /// Head of Household, somebody on the block - has no vote to share. One a week for each
        /// partner, however many deals; nothing for a player out of the house; no memory, as with the
        /// asked claim: a memory naming somebody counts in the web's vote term.
        /// </summary>
        private static void PassTheReadings(EpisodeState s)
        {
            var player = s.Find(s.playerId);
            if (player == null || player.status != ContestantStatus.Active || !VoteRead.Available(s)) return;
            var voters = new HashSet<string>(Voters(s).Where(v => !v.isPlayer).Select(v => v.id));
            var told = new HashSet<string>();
            foreach (var deal in s.deals.Where(d => d.status == DealStatus.Active && d.type == DealKind.InformationSharing).ToList())
            {
                string partner = DealResolution.Partner(deal, s.playerId);
                if (partner == null || !voters.Contains(partner) || !told.Add(partner)) continue;
                string truth = ProjectBallot(s, partner).selectedNomineeId;
                if (string.IsNullOrEmpty(truth)) continue;
                // As good as their word to anybody who asks: an ally's is the truth (C6, AnswerHonesty).
                bool honest = StoryRandom.Chance(s, ReadingKey(s, partner), AnswerHonesty(s, s.Find(partner)));
                string stated = honest ? truth : s.nominees.FirstOrDefault(id => id != truth);
                if (string.IsNullOrEmpty(stated)) continue;
                SeasonLedger.Append(s.ledger, s.ledger.claims, new ClaimRow { week = s.week, voterId = partner, targetId = stated, source = ClaimSource.Told });
                Log(s, "vote-read", ReadingLine(Name(s, partner), Target(s, stated, partner)), s.playerId, partner);
            }
        }

        /// <summary>The key of the coin an information partner's honesty is drawn on: the week and the partner, in the season's own story stream (<see cref="StoryRandom"/>).</summary>
        public static string ReadingKey(EpisodeState s, string partnerId) => "w" + s.week + ":reading:" + partnerId;

        /// <summary>
        /// At the reveal, under the commitment rules (C1): an information partner whose claim to the
        /// player's face was judged a lie has broken the deal - the truth was what it promised. Broken
        /// by them, settled as any breach is, its line to both: the judged claim has already told the
        /// player the ballot.
        /// </summary>
        private static void BreakTheDealsOfLyingPartners(EpisodeState s)
        {
            var liars = new HashSet<string>(s.ledger.claims
                .Where(k => k.week == s.week && k.status == ClaimStatus.Lied && k.source == ClaimSource.Told)
                .Select(k => k.voterId));
            if (liars.Count == 0) return;
            var verdicts = new List<DealResolution.Verdict>();
            foreach (var deal in s.deals.Where(d => d.status == DealStatus.Active && d.type == DealKind.InformationSharing))
            {
                string partner = DealResolution.Partner(deal, s.playerId);
                if (partner != null && liars.Contains(partner))
                    verdicts.Add(new DealResolution.Verdict { deal = deal, status = DealStatus.Broken, actorId = partner });
            }
            SettleDeals(s, verdicts);
        }

        /// <summary>The line an information deal's reading is told in: "Alex kept you in the loop, as your information deal has it: they're voting to evict Maya."</summary>
        public static string ReadingLine(string partnerName, string evicting) =>
            partnerName + " kept you in the loop, as your information deal has it: they're voting to evict " + evicting + ".";
    }
}
