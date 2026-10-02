using System;
using System.Collections.Generic;
using System.Linq;

namespace Gamesim.Simulation
{
    /// <summary>
    /// Whose pact with the player still holds from their own side (ACTIONS-DEALS-ALLIANCES-PLAN C2,
    /// C3 and X5), under the commitment rules (<see cref="EpisodeEngine.CommitmentRulesOn"/>). Without
    /// them every reader here reduces to what the season always read: a pact holds while it stands.
    ///
    /// <para><b>Betrayal (C2).</b> An ally who nominates the player or names them the replacement,
    /// breaks a veto commitment with them, or - at the reveal - votes to evict them, breaks a vote
    /// deal with them by their ballot, or refused the player's call and then voted the other way,
    /// turns on their pact. The engine writes it once a week for each ally, as the dormant
    /// <see cref="BetrayedType"/> entry on the player's own record of them - the reference's heaviest
    /// entry, −50, which never fades - and logs it. A refused call alone is no betrayal: it is the
    /// ballot that keeps or breaks it. From then the betrayer's protection terms stop counting - the
    /// shield, the veto pull, the vote and following a call - in every pact the player had joined no
    /// later than that week (a pact the player joins in a later week is a fresh commitment, and the
    /// betrayed pact stays betrayed). The player may cut ties that week at no cost
    /// (<see cref="FreeExit"/>), or keep the pact.</para>
    ///
    /// <para><b>The NPC's own commitment (C3).</b> Before the rules the player's pacts were one-sided:
    /// a partner's terms held as long as the pact did, and the pact ended only on the player's own view
    /// of them. Under them a partner's terms hold only while their own commitment does: they have not
    /// turned on it, and their own view of the player is not below <see cref="QuietLine"/>, the
    /// reference's line under which a member will not have the player in their alliance. Below
    /// <see cref="NpcAlliances.SourLine"/> they end it from their side (<see cref="NpcAlliances.Dissolve"/>).</para>
    ///
    /// <para><b>Leaving the house (X5).</b> A houseguest who leaves the house leaves every pact: they
    /// hold none of a pact's terms and count toward none of its size. The pact's own record keeps them
    /// as departed - the alliances page and the finale's receipts read who was in it - and a juror who
    /// was in a pact with a finalist reads as a former ally (<see cref="JuryLoyalty"/>).</para>
    ///
    /// <para><b>What the player sees.</b> A betrayal the player can know - a nomination, a veto
    /// commitment broken in front of the house, a ballot the count or a judged claim already shows -
    /// is logged to them with the offer of a free exit. One told only by a ballot they cannot place is
    /// logged to the betrayer alone, kept off every page of the player's while the ballot is not theirs
    /// to know (<see cref="KnownBallots.TellsAnUnknownBallot"/>; the vote read and the levers' lines
    /// project on <see cref="AsThePlayerKnows"/>), and opens its free exit only once it is. Where an
    /// ally stands is the player's to tell only this week, through a read, a refused call or contact
    /// (<see cref="CommitmentKnown"/>): then the notes say a cold ally has "gone quiet", in words and
    /// never a number, and the vote read counts the ally's pact terms. The two always agree.</para>
    ///
    /// <para>Pure and read-only: it neither mutates the state nor draws from its generator.</para>
    /// </summary>
    public static class Allegiance
    {
        /// <summary>The ledger's permanent entry for a betrayed alliance (<see cref="RelationshipLedger"/>), dormant until C2.</summary>
        public const string BetrayedType = "alliance-betrayed";

        /// <summary>What it weighs on the record: the reference's table, the heaviest single entry in it.</summary>
        public const double BetrayalImpact = -50;

        /// <summary>
        /// Below this, an ally's own commitment to the player has lapsed: the reference's hostility check
        /// (<see cref="StrategyRules.HostilityLine"/>), under which a member will not have the player in
        /// their alliance at all.
        /// </summary>
        public const double QuietLine = StrategyRules.HostilityLine;

        /// <summary>The kind of the line a betrayal writes to the log.</summary>
        public const string LogKind = "alliance-betrayal";

        // ------------------------------------------------------------ the acts, as the record says them

        public const string Nominated = "nominated you", NamedReplacement = "named you as the replacement nominee",
            VotedAgainst = "voted to evict you";

        /// <summary>A call refused and then voted against at the reveal: told by the ballot.</summary>
        public static string IgnoredCall(string targetName) => "ignored your call to evict " + targetName;

        public static string BrokeDeal(string type) => "broke a " + DealKind.Title(type).ToLowerInvariant() + " with you";

        /// <summary>The record of a betrayal, on the player's record of the betrayer: "Riley nominated you despite The Riley Pact."</summary>
        public static string Record(string name, string act, IEnumerable<string> pacts) => name + " " + act + " despite " + Join(pacts) + ".";

        /// <summary>The choice a betrayal the player knows of opens.</summary>
        public static string Offer(string name) => "You can cut ties with " + FirstName(name) + " this week at no cost, or stay allied.";

        /// <summary>
        /// The free exit's line for one pact: a pact of two is over; a bigger one goes on without the
        /// betrayer. "You cut ties with Riley Chen after they turned on The Riley Pact. It is over, and
        /// nobody holds it against you."
        /// </summary>
        public static string CutTiesLine(string name, string pact, bool ended) =>
            "You cut ties with " + name + " after they turned on " + pact + ". "
            + (ended ? "It is over" : FirstName(name) + " is out of it, the rest of you keep it") + ", and nobody holds it against you.";

        // ------------------------------------------------------------ the record of a betrayal

        /// <summary>The latest week this houseguest turned on a pact of theirs with the player, or 0 when they never did.</summary>
        public static int BetrayalWeek(EpisodeState s, string npcId) => LatestWeek(BetrayalEntries(s, npcId));

        /// <summary>
        /// The week the player has been in a pact since: the week they joined it, as the finalist cards
        /// and the alliances page date it (<see cref="FinalistRead.PlayerAlliedSince"/>); the pact's own
        /// start where the record cannot say when they joined; this week for a pact too new to have a row.
        /// </summary>
        public static int StartWeek(EpisodeState s, AllianceState alliance)
        {
            if (s == null || alliance == null) return 0;
            int joined = FinalistRead.PlayerAlliedSince(s, alliance);
            if (joined > 0) return joined;
            var row = s.ledger?.alliances?.FirstOrDefault(r => r != null && r.id == alliance.id);
            return row != null && row.startedWeek > 0 ? row.startedWeek : s.week;
        }

        /// <summary>Whether a betrayal of this houseguest's stands against this pact: one in a week no earlier than the player joined it.</summary>
        public static bool Stands(EpisodeState s, string npcId, AllianceState alliance)
        {
            int week = BetrayalWeek(s, npcId);
            return week > 0 && alliance != null && StartWeek(s, alliance) <= week;
        }

        /// <summary>
        /// Whether this houseguest has turned on the player: a betrayal stands against every pact the two
        /// share, so no fresh commitment has been made since.
        /// </summary>
        public static bool Betrayed(EpisodeState s, string npcId) => StandsAgainstEveryPact(s, npcId, BetrayalWeek(s, npcId));

        /// <summary>The same, by the betrayals the player can know: what the player can tell has turned on them.</summary>
        public static bool KnownBetrayed(EpisodeState s, string npcId) =>
            EpisodeEngine.CommitmentRulesOn(s) && StandsAgainstEveryPact(s, npcId, LatestWeek(KnownBetrayals(s, npcId)));

        // ------------------------------------------------------------ commitment

        /// <summary>Whether somebody is still in the house.</summary>
        public static bool InHouse(EpisodeState s, string id) => s?.Find(id)?.status == ContestantStatus.Active;

        /// <summary>
        /// Whether this houseguest's own commitment to their pact with the player has lapsed, under the
        /// commitment rules: they have left the house, turned on it, or their own view of the player has
        /// fallen below <see cref="QuietLine"/>. Never without the rules, and never the player's own.
        /// </summary>
        public static bool Lapsed(EpisodeState s, string npcId)
        {
            if (!EpisodeEngine.CommitmentRulesOn(s) || string.IsNullOrEmpty(npcId) || npcId == s.playerId) return false;
            return !InHouse(s, npcId) || Betrayed(s, npcId) || s.Score(npcId, s.playerId) < QuietLine;
        }

        /// <summary>
        /// Whether <paramref name="fromId"/>'s alliance terms toward <paramref name="toId"/> count: the
        /// shield at the nominations, the pull at the veto, a plea's "you're allied" and a deal's. Without
        /// the rules, whether they share a standing pact, as it always was. Under them both must be in
        /// the house (<see cref="EpisodeState.Allied"/>), and a houseguest's terms toward the player hold
        /// only while their own commitment does (<see cref="Lapsed"/>). The player's own choices are the player's.
        /// </summary>
        public static bool Holds(EpisodeState s, string fromId, string toId)
        {
            if (s == null || string.IsNullOrEmpty(fromId) || string.IsNullOrEmpty(toId) || !s.Allied(fromId, toId)) return false;
            if (!EpisodeEngine.CommitmentRulesOn(s)) return true;
            return toId != s.playerId || fromId == s.playerId || !Lapsed(s, fromId);
        }

        /// <summary>
        /// Whether the player can tell where an ally stands with them right now, under the commitment
        /// rules: they read the ally this week, the ally refused their call this week, the two had
        /// contact this week (the player's own record of them was touched), or a betrayal of the ally's
        /// that the player can know stands against their pacts. Only then does the vote read count the
        /// ally's pact terms (<see cref="VoteRead.CommitmentHidden"/>) and only then do the notes say a
        /// cold ally has gone quiet (<see cref="GoneQuiet"/>): one rule, so the two agree.
        /// </summary>
        public static bool CommitmentKnown(EpisodeState s, string npcId)
        {
            if (!EpisodeEngine.CommitmentRulesOn(s) || string.IsNullOrEmpty(npcId) || npcId == s.playerId) return false;
            if (s.ledger?.standings != null && s.ledger.standings.Any(r => r != null && r.fromId == npcId && r.toId == s.playerId
                    && r.source == ClaimSource.Read && r.week == s.week)) return true;
            if (s.ledger?.calls != null && s.ledger.calls.Any(k => k != null && k.week == s.week && k.callerId == s.playerId
                    && k.defected != null && k.defected.Contains(npcId))) return true;
            var edge = s.relationships.FirstOrDefault(r => r.fromId == s.playerId && r.toId == npcId);
            if (edge != null && edge.lastInteractionWeek >= s.week) return true;
            return KnownBetrayed(s, npcId);
        }

        /// <summary>
        /// Whether an ally of the player's has gone quiet on them, as the player can tell it: in a pact
        /// with them, both in the house, the player in touch with them this week (<see cref="CommitmentKnown"/>),
        /// and their own view of the player under <see cref="QuietLine"/>. The notes say it in words
        /// (<see cref="HouseguestNotes"/>); nothing says by how much.
        /// </summary>
        public static bool GoneQuiet(EpisodeState s, string npcId) =>
            EpisodeEngine.CommitmentRulesOn(s) && !string.IsNullOrEmpty(npcId) && npcId != s.playerId
            && s.Allied(s.playerId, npcId) && CommitmentKnown(s, npcId) && s.Score(npcId, s.playerId) < QuietLine;

        /// <summary>
        /// <see cref="Holds"/> toward the player as the player can know it, for the odds they are shown
        /// (<see cref="KnownOdds"/>): the pact they know of holds unless they know of a betrayal that
        /// stands, or can tell this week that the ally has gone quiet. A betrayal they cannot know, and a
        /// view they cannot see, never move it. Always so without the rules.
        /// </summary>
        public static bool HoldsAsKnown(EpisodeState s, string npcId)
        {
            if (!EpisodeEngine.CommitmentRulesOn(s)) return true;
            if (KnownBetrayed(s, npcId)) return false;
            return !(CommitmentKnown(s, npcId) && s.Score(npcId, s.playerId) < QuietLine);
        }

        /// <summary>
        /// The members a pact counts, for its size in threat and in the vote: under the commitment rules
        /// only those still in the house (X5); without them every member, as it always was.
        /// </summary>
        public static List<string> Counted(EpisodeState s, AllianceState alliance) =>
            EpisodeEngine.CommitmentRulesOn(s)
                ? alliance.members.Where(id => InHouse(s, id)).ToList()
                : new List<string>(alliance.members);

        /// <summary>
        /// A pact's members whose own commitment to the player counts for nothing in it, under the
        /// commitment rules: a pact of the player's only, while the player is in the house (once they
        /// are gone it is a pact among houseguests), and only those still in it - each one lapsed
        /// (<see cref="Lapsed"/>), or with a betrayal that stands against this pact, which a fresh pact
        /// made with them later does not undo. Empty without the rules.
        /// </summary>
        public static List<string> LapsedMembers(EpisodeState s, AllianceState alliance)
        {
            if (!EpisodeEngine.CommitmentRulesOn(s) || alliance?.members == null || !alliance.members.Contains(s.playerId)
                || !InHouse(s, s.playerId))
                return new List<string>();
            return alliance.members.Where(id => id != s.playerId && InHouse(s, id) && (Lapsed(s, id) || Stands(s, id, alliance))).ToList();
        }

        /// <summary>
        /// The members who answer to a pact's bloc, for the vote's coordination: under the commitment
        /// rules a pact of the player's leaves out the members whose own commitment to the player counts
        /// for nothing in it (C3), who no longer follow it. Every member otherwise, as it always was.
        /// </summary>
        public static List<string> Following(EpisodeState s, AllianceState alliance)
        {
            var lapsed = LapsedMembers(s, alliance);
            return alliance.members.Where(id => !lapsed.Contains(id)).ToList();
        }

        /// <summary>
        /// What a pact with a finalist is worth to a juror, under the commitment rules (C3, X5): 100 while
        /// the juror's own commitment holds (<see cref="Holds"/>), which it never does once they have
        /// left the house; 25 for a pact the two shared, ended or left behind with the house; 0 for none,
        /// or for one the juror turned on the player in.
        /// </summary>
        public static double JuryLoyalty(EpisodeState s, string jurorId, string finalistId)
        {
            if (s == null) return 0;
            if (Holds(s, jurorId, finalistId)) return 100;
            bool shared = s.alliances.Any(a => a.members.Contains(jurorId) && a.members.Contains(finalistId)
                                               && !(finalistId == s.playerId && Stands(s, jurorId, a)));
            return shared ? 25 : 0;
        }

        /// <summary>
        /// Whether a juror and a finalist were in it together to the end, for the juror's "we were in
        /// this together" (<see cref="WebJuryVoting.Reason"/>), under the commitment rules: the finale
        /// questions' rule - a pact that still stands, or that ended only because the juror left the
        /// house - less one the juror turned on the player in.
        /// </summary>
        public static bool SharedToTheEnd(EpisodeState s, string jurorId, string finalistId)
        {
            if (s == null) return false;
            int? left = JuryHouseRead.LeftWeek(s, jurorId);
            return s.alliances.Any(a => a.members.Contains(jurorId) && a.members.Contains(finalistId)
                && !(finalistId == s.playerId && Stands(s, jurorId, a))
                && (a.active || (s.ledger?.alliances != null && s.ledger.alliances.Any(r => r != null && r.id == a.id && r.why != null
                    && r.why.EndsWith("/left-house", StringComparison.Ordinal) && left != null && r.endedWeek == left))));
        }

        // ------------------------------------------------------------ what the player can know

        /// <summary>
        /// Whether a line on the player's record tells them a ballot: a betrayal told by one - the ally's
        /// vote to evict them, a call they refused and then voted against, or a vote deal they broke with
        /// them by their ballot. A reader that prints such a line leaves it out while the ballot is not
        /// the player's to know (<see cref="KnownBallots.TellsAnUnknownBallot"/>).
        /// </summary>
        public static bool TellsABallot(EpisodeState s, string npcId, string text)
        {
            var npc = s?.Find(npcId);
            if (npc == null || string.IsNullOrEmpty(text) || string.IsNullOrEmpty(npc.name)) return false;
            if (text.StartsWith(npc.name + " " + VotedAgainst + " despite ", StringComparison.Ordinal)) return true;
            if (text.StartsWith(npc.name + " " + IgnoredCall(""), StringComparison.Ordinal)) return true;
            foreach (var kind in new[] { DealKind.VoteSave, DealKind.VoteEvict, DealKind.VoteTogether })
                foreach (var spelling in DealKind.Titles(kind))
                    if (text.StartsWith(npc.name + " broke a " + spelling.ToLowerInvariant() + " with you despite ", StringComparison.Ordinal))
                        return true;
            return false;
        }

        /// <summary>Every betrayal of this houseguest's the player can know, oldest first.</summary>
        public static List<RelationshipEventState> KnownBetrayals(EpisodeState s, string npcId) =>
            BetrayalEntries(s, npcId).Where(e => !KnownBallots.TellsAnUnknownBallot(s, npcId, e.description, e.week)).ToList();

        /// <summary>
        /// The season as the player knows it, for a page computed from what the player knows - the vote
        /// read and the levers' lines (M1 of the C2/C3 review): the state itself when every betrayal on
        /// the player's record is one they can know, and otherwise a copy without the ones a ballot they
        /// cannot place would tell, so a hidden ballot betrayal moves no read, no lean and no bloc. The
        /// engine's decisions, the ballots themselves and what a voter says when asked keep the true state.
        /// </summary>
        public static EpisodeState AsThePlayerKnows(EpisodeState s)
        {
            if (!EpisodeEngine.CommitmentRulesOn(s) || s.relationships == null) return s;
            bool hidden = s.relationships.Any(edge => edge.fromId == s.playerId && edge.events != null
                && edge.events.Any(e => e != null && e.type == BetrayedType && KnownBallots.TellsAnUnknownBallot(s, edge.toId, e.description, e.week)));
            if (!hidden) return s;
            var copy = s.Clone();
            foreach (var edge in copy.relationships.Where(r => r.fromId == copy.playerId && r.events != null))
                edge.events.RemoveAll(e => e != null && e.type == BetrayedType && KnownBallots.TellsAnUnknownBallot(s, edge.toId, e.description, e.week));
            return copy;
        }

        /// <summary>
        /// Whether cutting ties with this houseguest is free right now (C2): under the commitment rules,
        /// in the week they turned on a pact the player still shares with them, once the player can know
        /// it. No action, no warmth and no grudge from anyone; anything else is the leave it always was.
        /// </summary>
        public static bool FreeExit(EpisodeState s, string npcId)
        {
            if (!EpisodeEngine.CommitmentRulesOn(s) || string.IsNullOrEmpty(npcId) || npcId == s.playerId) return false;
            var npc = s.Find(npcId);
            if (npc == null || npc.isPlayer || !InHouse(s, s.playerId) || !s.Allied(s.playerId, npcId)) return false;
            return KnownThisWeek(s, npcId);
        }

        /// <summary>Whether the free exit keeps a pact: one of three or more still in the house goes on without the betrayer.</summary>
        public static bool FreeExitKeepsAPact(EpisodeState s, string npcId) =>
            s?.alliances != null && s.alliances.Any(a => a.active && a.members.Contains(s.playerId) && a.members.Contains(npcId)
                                                         && a.members.Count(id => InHouse(s, id)) > 2);

        /// <summary>Whether this houseguest turned on a pact of the player's this week, in a way the player can know.</summary>
        public static bool KnownThisWeek(EpisodeState s, string npcId) =>
            EpisodeEngine.CommitmentRulesOn(s) && KnownBetrayals(s, npcId).Any(e => e.week == s.week);

        // ------------------------------------------------------------ helpers

        private static IEnumerable<RelationshipEventState> BetrayalEntries(EpisodeState s, string npcId)
        {
            if (s?.relationships == null || string.IsNullOrEmpty(npcId) || npcId == s.playerId) return Enumerable.Empty<RelationshipEventState>();
            var edge = s.relationships.FirstOrDefault(r => r.fromId == s.playerId && r.toId == npcId);
            return edge?.events == null ? Enumerable.Empty<RelationshipEventState>()
                : edge.events.Where(e => e != null && e.type == BetrayedType);
        }

        private static int LatestWeek(IEnumerable<RelationshipEventState> entries)
        {
            int week = 0;
            foreach (var entry in entries) if (entry.week > week) week = entry.week;
            return week;
        }

        /// <summary>A betrayal in <paramref name="week"/> stands against every pact the two share: none of them did the player join later.</summary>
        private static bool StandsAgainstEveryPact(EpisodeState s, string npcId, int week) =>
            week > 0 && !s.alliances.Any(a => a.active && a.members.Contains(s.playerId) && a.members.Contains(npcId) && StartWeek(s, a) > week);

        /// <summary>"A", "A and B", "A, B and C".</summary>
        public static string Join(IEnumerable<string> names)
        {
            var list = (names ?? Enumerable.Empty<string>()).Where(n => !string.IsNullOrEmpty(n)).ToList();
            if (list.Count <= 1) return list.FirstOrDefault() ?? "";
            return string.Join(", ", list.Take(list.Count - 1)) + " and " + list.Last();
        }

        private static string FirstName(string name) => string.IsNullOrEmpty(name) ? "" : name.Split(' ')[0];
    }
}
