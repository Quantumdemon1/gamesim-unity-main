using System;
using System.Collections.Generic;
using System.Linq;

namespace Gamesim.Simulation
{
    /// <summary>
    /// The war room (WAVE-D-NPC-PACTS-PLAN §3): the engine's half. The rules' numbers, words and
    /// decisions are <see cref="PactPlans"/>'; here is where they act.
    ///
    /// <para><b>The meeting.</b> Under the rules a pact of three or more in the house meets only once the
    /// block is set, once a week (its plan row is the attempt row), and its meeting is the war room
    /// wherever somebody of it votes and somebody at it answers the player as an ally: C6's meeting - the
    /// same warmth, the same draws - with the says in place of one ally's claim, and an open plan on the
    /// ledger (<see cref="HoldWarRoom"/>). Otherwise the meeting is C6's.</para>
    ///
    /// <para><b>The answer</b> (<see cref="EpisodeCommandKind.AnswerPactPlan"/>) is free and draws nothing
    /// from the season's stream: going with the plan, pushing for the other nominee once, or lying low.
    /// One line to the pact. A plan the player backs is the week's call in the player's name, a
    /// <see cref="BlocCallRow"/> as the levers write one; a plan an NPC leads is read by the bloc round
    /// from its row (<see cref="PlanCallThisWeek"/>), since every call row is the player's to its readers.</para>
    ///
    /// <para><b>The lapse.</b> As the campaign closes, after the readings, every plan still open settles as
    /// a lie-low with the same coins, one line each (<see cref="LapsePactPlans"/>).</para>
    /// </summary>
    public sealed partial class EpisodeEngine
    {
        /// <summary>Switches the war rooms on from a week, no later than the week after the season's own (schema 28's boundary).</summary>
        public static void EnablePactPlans(EpisodeState s, int fromWeek = 1)
        {
            if (s == null) throw new ArgumentNullException(nameof(s));
            s.pactPlanRulesStartWeek = Math.Max(1, Math.Min(fromWeek, s.week + 1));
        }

        /// <summary>
        /// Whether the season plays the war rooms this week (X8): their start week reached, and the
        /// commitment rules and the levers on - a war room's plan is the week's call, which only the levers
        /// make, and who has a say is who answers the player as an ally, which only the commitment rules say.
        /// </summary>
        public static bool PactPlanRulesOn(EpisodeState s) =>
            s != null && s.pactPlanRulesStartWeek >= 1 && s.week >= s.pactPlanRulesStartWeek
            && CommitmentRulesOn(s) && LeverRulesOn(s);

        /// <summary>
        /// The call a plan an NPC leads makes in the bloc round this week, as a call row the round can read -
        /// built from the plan, never written to the ledger, whose call rows are all the player's: its NPC
        /// caller, its target and who is with it. Null without the rules, for a plan the player backs (its
        /// row is the ledger's own, <see cref="CallThisWeek"/>), and for one still open or void. Where the
        /// caller no longer follows the pact at the reveal, or the target has left the block, the round's
        /// guard falls back to the founder's round.
        /// </summary>
        public static BlocCallRow PlanCallThisWeek(EpisodeState s, string allianceId)
        {
            if (!PactPlanRulesOn(s)) return null;
            var row = PactPlans.ThisWeek(s, allianceId);
            if (row == null || string.IsNullOrEmpty(row.targetId) || string.IsNullOrEmpty(row.callerId) || row.callerId == s.playerId) return null;
            return new BlocCallRow
            {
                week = row.week, allianceId = row.allianceId, callerId = row.callerId, targetId = row.targetId,
                followed = new List<string>(row.followed ?? new List<string>()),
            };
        }

        /// <summary>
        /// The guards a meeting of a pact of three or more meets under the rules, before anything is spent
        /// (D3-M1): it waits for the block to be set, whatever pact the command names, and it meets once a
        /// week by its plan row as well as by C6's cooldown, which a full cooldown store can lose.
        /// </summary>
        private static void RequireWarRoomTiming(EpisodeState s, AllianceState pact)
        {
            if (!PactPlanRulesOn(s) || !PactPlans.IsWarRoomPact(s, pact)) return;
            Require(PactPlans.BlockSet(s), PactPlans.NotYetRefusal(pact.name));
            Require(PactPlans.ThisWeek(s, pact.id) == null, pact.name + " has already met this week.");
        }

        /// <summary>
        /// The war room, after C6's meeting has moved every pair at it: each member's say read from the
        /// state after the meeting, a plan opened on the ledger - who was there, who said what, who it was
        /// held through - and the meeting's line with the says in place of one ally's claim (§6 Q8). Nothing
        /// drawn, and no memory beyond the meeting's own nameless one. False, with nothing written, where
        /// nobody at it has a say: the meeting is then C6's.
        /// </summary>
        private static bool HoldWarRoom(EpisodeState s, AllianceState pact, ContestantState through, List<string> at)
        {
            var says = PactPlans.Says(s, pact);
            if (says.Count == 0) return false;
            SeasonLedger.Append(s.ledger, s.ledger.plans, new PactPlanRow
            {
                week = s.week, allianceId = pact.id, throughId = through.id, stance = PactPlanStance.Open,
                present = new[] { s.playerId }.Concat(at).ToList(), says = says,
            });
            Cool(s, MeetingKey(s, pact.id), s.week + 1);
            Log(s, "conversation", PactPlans.WarRoomLine(s, pact, at, says), new[] { s.playerId }.Concat(at).ToArray());
            return true;
        }

        /// <summary>
        /// The player's answer to a pact's plan (<see cref="EpisodeCommandKind.AnswerPactPlan"/>): to a member
        /// who was at the meeting (<c>targetId</c>), naming the pact (<c>text</c>) and a nominee
        /// (<c>secondTargetId</c>) - the plan, either on a split, the other to push for it - or nobody, to lie
        /// low. Free, as answering any offer is; refused before anything is spent, drawn or logged without
        /// the rules, outside the window, and once the plan is settled.
        /// </summary>
        private static void AnswerPactPlan(EpisodeState s, EpisodeCommand c)
        {
            Require(PactPlanRulesOn(s), WaveDKindRefusal);
            RequireConversationWindow(s, c);
            Require(s.Find(s.playerId).status == ContestantStatus.Active, "Evicted players can follow the season but cannot influence it.");
            string named = (c.text ?? string.Empty).Trim();
            var pact = named.Length == 0 ? null : s.alliances.FirstOrDefault(a => a.id == named);
            string chosen = string.IsNullOrWhiteSpace(c.secondTargetId) ? null : c.secondTargetId;
            string refusal = PactPlans.AnswerRefusal(s, pact, c.targetId, chosen);
            Require(refusal == null, refusal);
            var row = PactPlans.OpenPlan(s, pact.id);
            SettlePlan(s, pact, row, PactPlans.AnswerKind(s, pact, row, chosen), chosen);
        }

        /// <summary>
        /// Writes a plan's settlement (<see cref="PactPlans.Settle"/>) to its row - the stance, the target,
        /// the caller, the counter, who came round, who is with it, and anybody brought into the plan since
        /// the meeting - and, where the player calls it, the week's call row in their name; then the one
        /// line to the pact. A void plan names no target and makes no call.
        /// </summary>
        private static void SettlePlan(EpisodeState s, AllianceState pact, PactPlanRow row, string answer, string chosen)
        {
            var settled = PactPlans.Settle(s, pact, row, answer, chosen);
            row.stance = settled.stance;
            row.targetId = settled.targetId;
            row.callerId = settled.callerId;
            row.counterId = settled.Void ? null : settled.counterId;
            row.cameRound = new List<string>(settled.cameRound);
            row.followed = new List<string>(settled.followed);
            foreach (string id in settled.joined.Where(id => !row.present.Contains(id))) row.present.Add(id);
            if (!settled.Void && settled.callerId == s.playerId)
                SeasonLedger.Append(s.ledger, s.ledger.calls, new BlocCallRow
                {
                    week = s.week, allianceId = row.allianceId, callerId = s.playerId, targetId = settled.targetId,
                    followed = new List<string>(settled.followed),
                });
            var audience = new List<string> { s.playerId };
            if (pact?.members != null) audience.AddRange(pact.members.Where(id => id != s.playerId && Allegiance.InHouse(s, id)));
            Log(s, WaveDEventKinds.PactPlan, PactPlans.SettledLine(s, pact, null, settled, chosen), audience.Distinct().ToArray());
        }

        /// <summary>
        /// The campaign's close (§1, after <see cref="PassTheReadings"/>): every plan still open settles as the
        /// player lying low would, with the same coins, one line each - void where its pact has ended or no
        /// say stands. In the order the pacts met.
        /// </summary>
        private static void LapsePactPlans(EpisodeState s)
        {
            foreach (var row in s.ledger.plans.Where(p => p != null && p.stance == PactPlanStance.Open).ToList())
                SettlePlan(s, s.alliances.FirstOrDefault(a => a.id == row.allianceId), row, PactPlans.Lapse, null);
        }
    }
}
