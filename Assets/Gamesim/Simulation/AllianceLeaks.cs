using System;
using System.Collections.Generic;
using System.Linq;

namespace Gamesim.Simulation
{
    /// <summary>
    /// Leaks and double-dealing (WAVE-D-NPC-PACTS-PLAN §2, ACTIONS-DEALS-ALLIANCES-PLAN B4, decision 12A):
    /// the rules' numbers and words, pure. A secret pact's fact goes out as a whisper on one keyed coin a
    /// week (<see cref="Odds"/>, <see cref="Key"/>), and the house's own gossip carries it from there; an
    /// ally of the player's who learns of the player's <i>other</i> pact holds it against them
    /// (<see cref="IsCaughtOut"/>, <see cref="HasRival"/>) and the player is told in one line
    /// (<see cref="Line"/>); a listen-in on the two people of a pact makes the player a suspected
    /// knower of it (<see cref="ListenInSentence"/>). The engine's half is
    /// <c>EpisodeEngine.AllianceLeaks</c>.
    ///
    /// <para>Keyed to <see cref="EpisodeState.allianceLeakRulesStartWeek"/> (schema 28) and nothing
    /// else: before it a season plays exactly as it did, the two-name whisper, the over-grant and the
    /// listen-in's words included. The director starts every season under them, the importer from the
    /// week after an import; migration and recovery leave them off, and so does every season a test
    /// builds directly.</para>
    ///
    /// <para>Nothing here draws from the season's stream, writes an id or mutates the state: the coin is
    /// <see cref="StoryRandom"/>'s, keyed by the week and the pact, so it is order-independent and a
    /// reload replays it.</para>
    /// </summary>
    public static class AllianceLeaks
    {
        /// <summary>A secret pact's weekly leak odds: the base, every member past two, and the player's juggling (decision 12A).</summary>
        public const double BaseOdds = 0.05, PerMemberOdds = 0.03, JugglingOdds = 0.05;

        /// <summary>The odds never pass a half. With the player held to three pacts the cap never binds; it stays as a guard.</summary>
        public const double OddsCap = 0.5;

        /// <summary>A houseguest holds a rival in the pact they found out about at this grudge, or at this view or below.</summary>
        public const double RivalGrudge = 20, RivalScore = -10;

        /// <summary>What finding out about a pact with a rival in it leaves: the alliance-betrayed grudge, ×1.2 while allied (48), past C4's refusal line of 40.</summary>
        public const double BetrayedGrudge = 40;

        /// <summary>What finding out leaves otherwise: the permanent receipt, at its own impact (<see cref="StoryReceipts.DoubleDealt"/>).</summary>
        public const double ReceiptImpact = -10;

        /// <summary>
        /// Whether the season plays the leak rules this week: its start week reached, the story's knowledge
        /// (bonds and facts) running, and the commitment rules on - under which an alliance's fact is never
        /// pruned (<see cref="Knowledge.MakeRoom"/>) and nobody out of the house is allied
        /// (<see cref="EpisodeState.Allied"/>), so a reaction fires at most once per houseguest and pact.
        /// </summary>
        public static bool On(EpisodeState s) =>
            s != null && s.allianceLeakRulesStartWeek >= 1 && s.week >= s.allianceLeakRulesStartWeek
            && EpisodeEngine.StoryAt(s, StoryRules.Bonds) && EpisodeEngine.CommitmentRulesOn(s);

        // ------------------------------------------------------------ the weekly leak

        /// <summary>
        /// A pact's weekly odds: <c>min(0.5, 0.05 + 0.03·max(0, m−2) + J)</c>, where <paramref name="activeMembers"/>
        /// is m, and J is <c>0.05·max(0, k−1)</c> with k the player's pacts held - only for a pact the player
        /// is in and still in the house to juggle (<paramref name="playersPact"/>), never for one between others.
        /// </summary>
        public static double Odds(int activeMembers, int playerPacts, bool playersPact) =>
            Math.Min(OddsCap, BaseOdds + PerMemberOdds * Math.Max(0, activeMembers - 2)
                + (playersPact ? JugglingOdds * Math.Max(0, playerPacts - 1) : 0));

        /// <summary>This pact's odds this week, as the state stands.</summary>
        public static double Odds(EpisodeState s, AllianceState pact)
        {
            if (s == null || pact?.members == null) return 0;
            bool players = pact.members.Contains(s.playerId) && s.Find(s.playerId)?.status == ContestantStatus.Active;
            return Odds(ActiveMembers(s, pact), EpisodeEngine.PlayerPactsHeld(s), players);
        }

        /// <summary>The coin's key: one draw per week and pact, whatever order the pacts are taken in.</summary>
        public static string Key(EpisodeState s, AllianceState pact) => "w" + s.week + ":leak:" + pact.id;

        /// <summary>
        /// The fact a pact's coin is drawn for this week, or null when it draws none: a standing pact whose
        /// fact is still private and from an earlier week (a pact formed this week waits a week), with at
        /// least two of its members in the house, somebody in the house outside it, and four in the house.
        /// A whispered fact never rolls again: the gossip has it.
        /// </summary>
        public static HouseFactState Rolls(EpisodeState s, AllianceState pact)
        {
            if (s?.story == null || pact?.members == null || !pact.active) return null;
            var fact = Knowledge.Of(s, FactKinds.Alliance, pact.id);
            if (fact == null || fact.visibility != FactVisibility.Private || fact.week >= s.week) return null;
            var house = s.Active.ToList();
            if (house.Count < 4 || ActiveMembers(s, pact) < 2 || house.All(c => pact.members.Contains(c.id))) return null;
            return fact;
        }

        /// <summary>Whether the pact's fact goes out as a whisper this week: it rolls, under the rules, and its keyed coin lands under its odds.</summary>
        public static bool Leaks(EpisodeState s, AllianceState pact) =>
            On(s) && Rolls(s, pact) != null && StoryRandom.Chance(s, Key(s, pact), Odds(s, pact));

        /// <summary>The pact's members in the house.</summary>
        public static int ActiveMembers(EpisodeState s, AllianceState pact) =>
            pact?.members == null ? 0 : pact.members.Count(id => s.Find(id)?.status == ContestantStatus.Active);

        // ------------------------------------------------------------ double-dealing

        /// <summary>
        /// Whether a houseguest who has just come to know of a pact's fact has caught the player out: under
        /// the rules, a standing pact the player is in and still in the house to answer for, learned by
        /// somebody in the house who is not in it and who shares a pact with the player. Knowers only grow,
        /// so this holds once per houseguest and pact, and somebody who knew before they allied never does.
        /// </summary>
        public static bool IsCaughtOut(EpisodeState s, string knowerId, AllianceState pact)
        {
            if (!On(s) || pact?.members == null || !pact.active || !pact.members.Contains(s.playerId)) return false;
            if (s.Find(s.playerId)?.status != ContestantStatus.Active) return false;
            var knower = s.Find(knowerId);
            if (knower == null || knower.isPlayer || knower.status != ContestantStatus.Active || pact.members.Contains(knowerId)) return false;
            return s.Allied(knowerId, s.playerId);
        }

        /// <summary>
        /// Whether a houseguest has a rival among the pact's other members (WAVE-D-NPC-PACTS-PLAN §2.3,
        /// "some other member"): one they hold a grudge of twenty or more against, or think ten or more
        /// below nothing of. A member who has left the house is still in the pact - eviction takes nobody
        /// out of one - so the player kept a pact with them all the same. Deterministic, no draw.
        /// </summary>
        public static bool HasRival(EpisodeState s, string knowerId, AllianceState pact) =>
            pact?.members != null && pact.members.Any(id => id != s.playerId && id != knowerId
                && (Grudges.Severity(s, knowerId, id) >= RivalGrudge || s.Score(knowerId, id) <= RivalScore));

        /// <summary>
        /// The one line both reactions say, to the player and to the one who found out (D4-M1): "Riley Chen
        /// found out about The Jo Pact, your alliance with Jo Park. They won't forget you kept it from them."
        /// </summary>
        public static string Line(EpisodeState s, string knowerId, AllianceState pact) =>
            FoundOutPrefix(Name(s, knowerId), pact.name) + Partners(s, pact) + ". They won't forget you kept it from them.";

        /// <summary>How a double-dealing line begins, for a reader matching it to the pact by name.</summary>
        public static string FoundOutPrefix(string knowerName, string pactName) =>
            knowerName + " found out about " + pactName + ", your alliance with ";

        /// <summary>The record the receipt is written with, on the one who found out's view of the player.</summary>
        public static string HeardNote(EpisodeState s, AllianceState pact) =>
            "Found out you are also working with " + Partners(s, pact) + ".";

        /// <summary>
        /// Who the player works with in a pact: its members in the house other than the player, full names
        /// in the pact's order; everybody else in it on the record where nobody is left in the house.
        /// </summary>
        public static string Partners(EpisodeState s, AllianceState pact)
        {
            var others = pact.members.Where(id => id != s.playerId).ToList();
            var here = others.Where(id => s.Find(id)?.status == ContestantStatus.Active).ToList();
            return Names(s, here.Count > 0 ? here : others);
        }

        // ------------------------------------------------------------ what reaches the player

        /// <summary>
        /// The whisper a pact's fact reaches the player in under the rules, naming everyone in it in the
        /// pact's own order: "Word in the house: Riley Chen, Jo Park and Sam Lee are working together."
        /// For a pair it is the two-name whisper the season always said.
        /// </summary>
        public static string WhisperLine(EpisodeState s, AllianceState pact) =>
            "Word in the house: " + Names(s, pact.members) + " are working together.";

        /// <summary>
        /// What a listen-in that made the player a knower of a pact adds to its line: " From the way they
        /// talked, Riley Chen and Jo Park are working together." It names exactly the members the
        /// alliances page's card shows, in its order (<see cref="AllianceRead.Suspected"/>).
        /// </summary>
        public static string ListenInSentence(EpisodeState s, AllianceState pact) =>
            " From the way they talked, " + Names(s, s.contestants.Where(c => pact.members.Contains(c.id)).Select(c => c.id).ToList())
            + " are working together.";

        // ------------------------------------------------------------ the risk word (D4-6)

        /// <summary>How likely one of the player's pacts is to get out this week, in a word.</summary>
        public const string RiskLow = "low", RiskSome = "some", RiskHigh = "high";

        /// <summary>
        /// The player's own read of how likely a pact of theirs is to get out: its weekly odds as the
        /// player can work them out - from how many of it are in the house and how many pacts the player
        /// holds, both theirs to see - in a word: five or eight in a hundred <see cref="RiskLow"/>, ten to
        /// fifteen <see cref="RiskSome"/>, sixteen or more <see cref="RiskHigh"/>. Never from whether it has got
        /// out, which the player cannot know, so a pact already whispered reads as one still secret. Null
        /// where there is nothing to read: the rules off, a pact that has ended or is not the player's, or
        /// the player out of the house.
        /// </summary>
        public static string RiskWord(EpisodeState s, AllianceState pact)
        {
            if (!On(s) || pact?.members == null || !pact.active || !pact.members.Contains(s.playerId)) return null;
            if (s.Find(s.playerId)?.status != ContestantStatus.Active) return null;
            double odds = Odds(ActiveMembers(s, pact), EpisodeEngine.PlayerPactsHeld(s), true);
            return odds < 0.095 ? RiskLow : odds < 0.155 ? RiskSome : RiskHigh;
        }

        /// <summary>The card's line for the risk word: "Risk of word getting out: some."</summary>
        public static string RiskLine(string word) => word == null ? null : "Risk of word getting out: " + word + ".";

        /// <summary>Whether a line is a double-dealing line.</summary>
        public static bool IsDoubleDealingLine(EpisodeEvent e) => e != null && e.kind == WaveDEventKinds.DoubleDealing;

        private static string Name(EpisodeState s, string id) => s.Find(id)?.name ?? "Somebody";

        /// <summary>"Riley, Jo and Sam": full names in the given order, as the receipts say them.</summary>
        private static string Names(EpisodeState s, IList<string> ids)
        {
            var names = ids.Select(id => s.Find(id)?.name).Where(n => n != null).ToList();
            if (names.Count <= 1) return names.FirstOrDefault() ?? "someone";
            return string.Join(", ", names.Take(names.Count - 1)) + " and " + names.Last();
        }
    }
}
