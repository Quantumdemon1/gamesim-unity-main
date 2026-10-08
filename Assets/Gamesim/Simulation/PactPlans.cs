using System;
using System.Collections.Generic;
using System.Linq;

namespace Gamesim.Simulation
{
    /// <summary>
    /// The war room (WAVE-D-NPC-PACTS-PLAN §3, ACTIONS-DEALS-ALLIANCES-PLAN B3): the rules' numbers, words
    /// and decisions, pure. A pact of three or more in the house meets once the block is set; each member
    /// who answers the player as an ally says who they want out (<see cref="Says"/>), the rest keep quiet;
    /// the nominee most of them name is the members' plan (<see cref="MembersPlan"/>); and the player goes
    /// with it, counters it once, or lies low (<see cref="Settle"/>). The plan is the week's call: the
    /// members whose final say is its target are bound to it, and the rest decide on a keyed coin whether
    /// to go along. The engine's half is <c>EpisodeEngine.PactPlans</c>.
    ///
    /// <para>Keyed to <see cref="EpisodeState.pactPlanRulesStartWeek"/> (schema 28) through
    /// <see cref="EpisodeEngine.PactPlanRulesOn"/>, which needs the commitment rules and the levers too:
    /// before it a season meets, calls and votes exactly as it did.</para>
    ///
    /// <para>Nothing here draws from the season's stream, writes an id or mutates the state. Every coin is
    /// <see cref="StoryRandom"/>'s, keyed by the week, the pact and the member, so the order changes nothing
    /// and a reload replays it. Every gate on the player's side reads the season as the player knows it
    /// (<see cref="Allegiance.AsThePlayerKnows"/>), so a betrayal told only by a ballot the player cannot
    /// place moves no outcome (D3-H2).</para>
    /// </summary>
    public static class PactPlans
    {
        /// <summary>A pact meets as a war room with this many of it in the house, the player included.</summary>
        public const int WarRoomSize = 3;

        /// <summary>
        /// How far a counter can reach a member: eight at a view of the player of fifty or more, scaled down
        /// to nothing at zero; half as much again for the Loyal, nothing for the Sneaky; never past twelve.
        /// </summary>
        public const double ReachBase = 8, ReachFullView = 50, ReachLoyal = 1.5, ReachCap = 12;

        /// <summary>How a plan is answered: the player goes with it, counters it, or lies low; or nobody answers by the campaign's close.</summary>
        public const string Agree = "agree", Counter = "counter", LieLow = "low", Lapse = "lapse";

        // ------------------------------------------------------------ which pacts, and when

        /// <summary>
        /// Whether a pact meets as a war room: a standing pact of the player's with at least three of it in
        /// the house, the player among them. A pact down to two in the house is a pair again: it meets and
        /// calls as C6 and the levers always let it.
        /// </summary>
        public static bool IsWarRoomPact(EpisodeState s, AllianceState pact) =>
            s != null && pact?.members != null && pact.active && pact.members.Contains(s.playerId)
            && Allegiance.InHouse(s, s.playerId) && EpisodeEngine.InTheHouse(s, pact) >= WarRoomSize;

        /// <summary>Whether the block is set for a war room: the campaign, with two nominees standing.</summary>
        public static bool BlockSet(EpisodeState s) => s != null && s.phase == EpisodePhase.Campaign && VoteRead.Available(s);

        /// <summary>This week's plan of a pact, whatever its stance, or null.</summary>
        public static PactPlanRow ThisWeek(EpisodeState s, string allianceId) =>
            s?.ledger?.plans == null || string.IsNullOrEmpty(allianceId) ? null
                : s.ledger.plans.LastOrDefault(p => p != null && p.week == s.week && p.allianceId == allianceId);

        /// <summary>This week's plan of a pact while it waits for the player's answer, or null.</summary>
        public static PactPlanRow OpenPlan(EpisodeState s, string allianceId)
        {
            var row = ThisWeek(s, allianceId);
            return row != null && row.stance == PactPlanStance.Open ? row : null;
        }

        /// <summary>
        /// Whether a meeting of this pact now would be a war room, by what the player can see: the rules on,
        /// a pact of three or more, the block set, somebody of it besides the player voting this week, and
        /// neither a plan nor a call of it this week. Whether anybody at it will say is theirs
        /// (<see cref="Convenes"/>). For the meeting row's pill.
        /// </summary>
        public static bool CouldConvene(EpisodeState s, AllianceState pact) =>
            EpisodeEngine.PactPlanRulesOn(s) && IsWarRoomPact(s, pact) && BlockSet(s)
            && EpisodeEngine.Voters(s).Any(v => !v.isPlayer && pact.members.Contains(v.id))
            && ThisWeek(s, pact.id) == null
            && !s.ledger.calls.Any(k => k != null && k.week == s.week && k.allianceId == pact.id);

        /// <summary>Whether a meeting of this pact now is a war room: it could convene, and somebody at it has a say.</summary>
        public static bool Convenes(EpisodeState s, AllianceState pact) => CouldConvene(s, pact) && Says(s, pact).Count > 0;

        /// <summary>Why a pact of three or more cannot meet before the block is set (D3-M1).</summary>
        public static string NotYetRefusal(string pactName) => pactName + " meets once the block is set.";

        /// <summary>Why a pact of three or more takes no call outside its war room (§6 Q2).</summary>
        public static string CallRefusal(string pactName) => pactName + " settles its call when it meets.";

        /// <summary>Why a plan already answered cannot be answered again.</summary>
        public static string SettledRefusal(string pactName) => pactName + " has settled this week's plan.";

        /// <summary>Why a pact's plan cannot be answered when it has none this week.</summary>
        public static string NoPlanRefusal(string pactName) => pactName + " has no plan on the table.";

        /// <summary>Why an answer must go through somebody who was at the meeting.</summary>
        public const string ThroughRefusal = "Answer the plan with somebody who was at the meeting.";

        /// <summary>Why the plan's answer names somebody on the block, and never the player.</summary>
        public const string NomineeRefusal = "Name somebody on the block other than yourself.";

        /// <summary>Why the plan can be answered only while the block stands.</summary>
        public static string BlockRefusal(string pactName) => pactName + "'s plan is answered while the block stands.";

        /// <summary>Why nobody can be gone with or pushed for once every say has left the pact.</summary>
        public static string NoSayRefusal(string pactName) => "Nobody in " + pactName + " has a say left.";

        // ------------------------------------------------------------ the says

        /// <summary>
        /// What a member says at a war room: who they want out, where they answer the player as an ally
        /// (<see cref="EpisodeEngine.SharesIntel"/>) - a voter or the Head of Household their own lean
        /// without the bloc (<see cref="WebEvictionVoting.EvaluateNative"/>), a nominee the other nominee.
        /// Null for a member who keeps quiet: one gone cold, or whose betrayal the player knows of. No draw.
        /// </summary>
        public static string SayOf(EpisodeState s, string memberId)
        {
            if (s?.nominees == null || s.nominees.Count != 2 || !EpisodeEngine.SharesIntel(s, memberId)) return null;
            if (s.nominees.Contains(memberId)) return s.nominees.First(id => id != memberId);
            return WebEvictionVoting.EvaluateNative(s, memberId).selectedNomineeId;
        }

        /// <summary>Every say at a meeting of this pact, in the pact's order (<see cref="EpisodeEngine.AtTheMeeting"/>): who said whom.</summary>
        public static List<PlanSay> Says(EpisodeState s, AllianceState pact)
        {
            var says = new List<PlanSay>();
            if (s?.nominees == null || s.nominees.Count != 2 || pact == null) return says;
            foreach (string id in EpisodeEngine.AtTheMeeting(s, pact))
            {
                string say = SayOf(s, id);
                if (!string.IsNullOrEmpty(say)) says.Add(new PlanSay { memberId = id, targetId = say });
            }
            return says;
        }

        /// <summary>
        /// The members' plan: the nominee more of the says name, or null on a split (as many for each) or
        /// with nobody saying. Quiet members, and a player lying low, cast none (§6 Q3).
        /// </summary>
        public static string MembersPlan(IEnumerable<PlanSay> says)
        {
            var counts = (says ?? Enumerable.Empty<PlanSay>()).Where(say => say != null && !string.IsNullOrEmpty(say.targetId))
                .GroupBy(say => say.targetId).Select(g => (target: g.Key, n: g.Count())).OrderByDescending(x => x.n).ToList();
            if (counts.Count == 0) return null;
            return counts.Count == 1 || counts[0].n > counts[1].n ? counts[0].target : null;
        }

        /// <summary>Whether the says split evenly: somebody said, and no nominee has more of them.</summary>
        public static bool IsSplit(IEnumerable<PlanSay> says)
        {
            var list = (says ?? Enumerable.Empty<PlanSay>()).Where(say => say != null && !string.IsNullOrEmpty(say.targetId)).ToList();
            return list.Count > 0 && MembersPlan(list) == null;
        }

        /// <summary>The members at a meeting who still count when the plan is answered: there, still in the pact and still in the house, in the order they met.</summary>
        public static List<string> StillHere(EpisodeState s, AllianceState pact, PactPlanRow row) =>
            row?.present == null || pact?.members == null || !pact.active ? new List<string>()
                : row.present.Where(id => id != s.playerId && pact.members.Contains(id) && Allegiance.InHouse(s, id)).Distinct().ToList();

        /// <summary>The says that still stand: those of the members still here (<see cref="StillHere"/>). A member cut out loses theirs.</summary>
        public static List<PlanSay> StandingSays(EpisodeState s, AllianceState pact, PactPlanRow row)
        {
            var here = StillHere(s, pact, row);
            return row?.says == null ? new List<PlanSay>() : row.says.Where(say => say != null && here.Contains(say.memberId)).ToList();
        }

        // ------------------------------------------------------------ the counter

        /// <summary>
        /// How far a counter reaches a member: <see cref="ReachBase"/> scaled by their view of the player
        /// (nothing at zero or less, whole at <see cref="ReachFullView"/>), Loyal ×1.5, Sneaky ×0, at most
        /// <see cref="ReachCap"/>. The obligation's sizing (<see cref="EpisodeEngine.Obligations"/>).
        /// </summary>
        public static double CounterReach(double viewOfPlayer, IList<string> traits)
        {
            double scale = Math.Max(0, Math.Min(1, viewOfPlayer / ReachFullView));
            double word = traits != null && traits.Contains("Sneaky") ? 0 : traits != null && traits.Contains("Loyal") ? ReachLoyal : 1;
            return Math.Min(ReachCap, ReachBase * scale * word);
        }

        /// <summary>The odds a member comes round: <c>reach ≤ 0 ? 0 : clamp01((reach − margin) / reach)</c>, where the margin is how firmly they lean.</summary>
        public static double ComeRoundOdds(double reach, double margin) =>
            reach <= 0 ? 0 : Math.Max(0, Math.Min(1, (reach - margin) / reach));

        /// <summary>The coin a member comes round on: one per week, pact and member.</summary>
        public static string CounterKey(EpisodeState s, string pactId, string memberId) => "w" + s.week + ":pact-counter:" + pactId + ":" + memberId;

        /// <summary>The coin a dissenter goes along on: one per week, pact and member.</summary>
        public static string PlanKey(EpisodeState s, string pactId, string memberId) => "w" + s.week + ":pact-plan:" + pactId + ":" + memberId;

        /// <summary>
        /// A member's odds of coming round, on the season as the player knows it: the reach of their view
        /// of the player against the margin of their own lean as it stands (D3-H2: a betrayal the player
        /// cannot see moves neither).
        /// </summary>
        public static double ComeRoundOdds(EpisodeState s, string memberId)
        {
            var known = Allegiance.AsThePlayerKnows(s);
            var member = known.Find(memberId);
            if (member == null || known.nominees == null || known.nominees.Count != 2) return 0;
            return ComeRoundOdds(CounterReach(known.Score(memberId, known.playerId), member.traits),
                WebEvictionVoting.EvaluateNative(known, memberId).margin);
        }

        /// <summary>
        /// Whether a member who said the members' plan comes round to the player's counter: not a nominee,
        /// not lapsed as the player knows the season (D3-H2), and their keyed coin under their odds - never
        /// the season's stream.
        /// </summary>
        public static bool ComesRound(EpisodeState s, string pactId, string memberId)
        {
            if (s.nominees.Contains(memberId) || Allegiance.Lapsed(Allegiance.AsThePlayerKnows(s), memberId)) return false;
            double odds = ComeRoundOdds(s, memberId);
            return odds > 0 && StoryRandom.Unit(s, CounterKey(s, pactId, memberId)) < odds;
        }

        // ------------------------------------------------------------ the answer

        /// <summary>
        /// Why the player cannot answer this pact's plan this way now, or null when they can - the checks
        /// after the rules, the window and the player's own place in the house: their pact, its open plan
        /// this week, the block standing, a member who was at the meeting to say it to, and a nominee other
        /// than themselves or none, to lie low.
        /// </summary>
        public static string AnswerRefusal(EpisodeState s, AllianceState pact, string throughId, string nomineeId)
        {
            if (pact == null || pact.members == null || !pact.active || !pact.members.Contains(s.playerId)) return EpisodeEngine.NotYourPactRefusal;
            var row = ThisWeek(s, pact.id);
            if (row == null) return NoPlanRefusal(pact.name);
            if (row.stance != PactPlanStance.Open) return SettledRefusal(pact.name);
            if (!BlockSet(s)) return BlockRefusal(pact.name);
            if (string.IsNullOrEmpty(throughId) || !StillHere(s, pact, row).Contains(throughId)) return ThroughRefusal;
            if (string.IsNullOrEmpty(nomineeId)) return null;
            if (nomineeId == s.playerId || !s.nominees.Contains(nomineeId)) return NomineeRefusal;
            if (StandingSays(s, pact, row).Count == 0) return NoSayRefusal(pact.name);
            return null;
        }

        /// <summary>
        /// What naming this nominee is, against the says that still stand: going with the members' plan, or
        /// either nominee on a split; pushing for the other one (the counter); naming nobody, lying low.
        /// </summary>
        public static string AnswerKind(EpisodeState s, AllianceState pact, PactPlanRow row, string nomineeId)
        {
            if (string.IsNullOrEmpty(nomineeId)) return LieLow;
            string plan = MembersPlan(StandingSays(s, pact, row));
            return plan == null || plan == nomineeId ? Agree : Counter;
        }

        /// <summary>How a plan settled: its stance, its target and caller (none when void), the counter, who came round, who is with it, and who joined it since the meeting.</summary>
        public sealed class Settlement
        {
            public string stance, targetId, callerId, counterId, membersPlanId;
            public List<string> cameRound = new List<string>(), followed = new List<string>(), joined = new List<string>();
            /// <summary>The members offered the call who voted: those bound to it and those who decided, in the pact's order.</summary>
            public List<string> offered = new List<string>();
            /// <summary>Each member still here and the nominee they ended on, in the order they met.</summary>
            public List<PlanSay> finalSays = new List<PlanSay>();
            public bool Void => stance == PactPlanStance.Void;
        }

        /// <summary>
        /// Settles an open plan by the player's answer, or by the campaign's close (<see cref="Lapse"/>),
        /// from the state as it stands - computed, never applied. The members' plan is the nominee most of
        /// the standing says name.
        /// <list type="bullet">
        /// <item><b>Agree:</b> the nominee named - the plan, or either on a split - and the player calls it.</item>
        /// <item><b>Counter:</b> each member who said the plan comes round on <see cref="ComesRound"/>; then the
        /// player, those who came round and those who already said the counter against those still on the
        /// plan. More says wins; a tie goes to the founder's side - the player where they founded the pact,
        /// else the NPC founder's final say - and with no founder here the plan stands. The player calls it
        /// only where the counter carried.</item>
        /// <item><b>Lie low, and the lapse:</b> the plan, or on a split the NPC founder's say, else the first
        /// in the pact's order; an NPC calls it. Void - no target, no call - where the pact has ended, no say
        /// stands, or the block no longer stands as the plan named it.</item>
        /// </list>
        /// Bound: every member who votes and ended on the target, with no draw. Every other member who votes
        /// (a dissenter, a quiet member, somebody brought in since) goes along only where they have not lapsed
        /// as the player knows the season and the bloc round's compliance rule says so on their keyed coin.
        /// An NPC caller is the founder where they ended on the target, else the first who did.
        /// </summary>
        public static Settlement Settle(EpisodeState s, AllianceState pact, PactPlanRow row, string answer, string chosenId)
        {
            var result = new Settlement();
            var here = StillHere(s, pact, row);
            var standing = StandingSays(s, pact, row);
            string plan = MembersPlan(standing);
            result.membersPlanId = plan;
            string Said(string id) => standing.FirstOrDefault(say => say.memberId == id)?.targetId;
            string founder = EpisodeEngine.Founder(pact);
            bool blockStands = s.nominees != null && s.nominees.Count == 2;

            string target;
            if (answer == Agree) target = chosenId;
            else if (answer == Counter)
            {
                result.counterId = chosenId;
                foreach (string id in here.Where(id => Said(id) == plan))
                    if (ComesRound(s, pact.id, id)) result.cameRound.Add(id);
                int forCounter = 1 + result.cameRound.Count + standing.Count(say => say.targetId == chosenId);
                int forPlan = standing.Count(say => say.targetId == plan) - result.cameRound.Count;
                if (forCounter != forPlan) target = forCounter > forPlan ? chosenId : plan;
                else if (EpisodeEngine.PlayerFounded(s, pact)) target = chosenId;
                else if (founder != null && Said(founder) != null) target = result.cameRound.Contains(founder) ? chosenId : Said(founder);
                else target = plan;
            }
            else target = plan ?? (standing.Count == 0 ? null : Said(founder) ?? standing[0].targetId);

            bool settles = pact != null && pact.active && standing.Count > 0 && blockStands && target != null && s.nominees.Contains(target);
            if (!settles)
            {
                result.stance = PactPlanStance.Void;
                result.cameRound.Clear();
                return result;
            }
            result.stance = answer == Agree ? PactPlanStance.Agreed : answer == Counter ? PactPlanStance.Countered
                : answer == LieLow ? PactPlanStance.Low : PactPlanStance.Lapsed;
            result.targetId = target;
            foreach (string id in here)
            {
                string said = result.cameRound.Contains(id) ? chosenId : Said(id);
                if (said != null) result.finalSays.Add(new PlanSay { memberId = id, targetId = said });
            }
            var bound = result.finalSays.Where(say => say.targetId == target).Select(say => say.memberId).ToList();
            bool playerCalls = (answer == Agree || answer == Counter) && target == chosenId;
            result.callerId = playerCalls ? s.playerId : bound.Contains(founder) ? founder : bound.FirstOrDefault();
            if (result.callerId == null) { result.stance = PactPlanStance.Void; result.targetId = null; result.cameRound.Clear(); result.finalSays.Clear(); return result; }

            // Who votes: everybody of the pact in the house who casts a ballot this week, in the pact's order.
            var voters = new HashSet<string>(EpisodeEngine.Voters(s).Where(v => !v.isPlayer).Select(v => v.id));
            var members = pact.members.Where(id => id != s.playerId && voters.Contains(id) && Allegiance.InHouse(s, id)).ToList();
            var known = Allegiance.AsThePlayerKnows(s);
            WebBlocSnapshot snapshot = null;
            WebBlocAlliance bloc = null;
            Func<string, string, double> trust = null;
            foreach (string id in members)
            {
                result.offered.Add(id);
                if (!row.present.Contains(id)) result.joined.Add(id);
                if (bound.Contains(id)) { result.followed.Add(id); continue; }
                if (Allegiance.Lapsed(known, id)) continue;
                if (snapshot == null)
                {
                    snapshot = WebVotingBlocs.FromNative(known);
                    bloc = snapshot.alliances.FirstOrDefault(a => a.id == pact.id);
                    trust = WebVotingBlocs.ProxyTrust(snapshot);
                }
                if (bloc == null) continue;
                string member = id;
                double loyalty = WebVotingBlocs.Loyalty(snapshot, bloc, member, result.callerId, target, trust);
                var traits = s.Find(member)?.traits ?? new List<string>();
                if (WebVotingBlocs.Complies(loyalty, traits, () => StoryRandom.Unit(s, PlanKey(s, pact.id, member)))) result.followed.Add(member);
            }
            return result;
        }

        /// <summary>
        /// The members a plan-backed call counted against, by the record (D3-L1): those at the meeting, or
        /// brought into the plan since, who voted that week and are not among those with it. The week's
        /// power row says who held the house and who stood on the block; before its reveal, the week's own
        /// voters.
        /// </summary>
        public static List<string> NotFollowing(EpisodeState s, PactPlanRow row)
        {
            if (row?.present == null) return new List<string>();
            var power = s.ledger?.power?.LastOrDefault(p => p != null && p.week == row.week);
            bool revealed = power?.nominees != null && power.nominees.Count == 2;
            var live = row.week == s.week ? new HashSet<string>(EpisodeEngine.Voters(s).Select(v => v.id)) : null;
            return row.present.Where(id => id != s.playerId && !(row.followed ?? new List<string>()).Contains(id)
                && (revealed ? id != power.hohId && !power.nominees.Contains(id) : live == null || live.Contains(id))).ToList();
        }

        // ------------------------------------------------------------ the readers (D3-S7)

        /// <summary>The plan a call row is, where a war room's plan made it - that week's plan of the pact - or null for a call the levers made.</summary>
        public static PactPlanRow PlanOf(EpisodeState s, BlocCallRow call) =>
            call == null || s?.ledger?.plans == null ? null
                : s.ledger.plans.LastOrDefault(p => p != null && p.week == call.week && p.allianceId == call.allianceId);

        /// <summary>A week's plans, in the order the pacts met; none without the war rooms.</summary>
        public static List<PactPlanRow> OfWeek(EpisodeState s, int week) =>
            s?.ledger?.plans == null ? new List<PactPlanRow>() : s.ledger.plans.Where(p => p != null && p.week == week).ToList();

        /// <summary>Whether a plan settled as the player's call: they went with it, or pushed for the other nominee and it carried.</summary>
        public static bool PlayerCalled(EpisodeState s, PactPlanRow row) =>
            s != null && row != null && !string.IsNullOrEmpty(row.targetId) && row.callerId == s.playerId;

        /// <summary>The nominee a member ended a plan on: the counter where they came round to it, else what they said at the meeting; null for one who kept quiet, or came into the plan since.</summary>
        public static string FinalSay(PactPlanRow row, string memberId)
        {
            if (row == null || string.IsNullOrEmpty(memberId)) return null;
            if (row.cameRound != null && row.cameRound.Contains(memberId) && !string.IsNullOrEmpty(row.counterId)) return row.counterId;
            string said = row.says?.FirstOrDefault(say => say != null && say.memberId == memberId)?.targetId;
            return string.IsNullOrEmpty(said) ? null : said;
        }

        /// <summary>
        /// The members of a settled plan who voted that week, in the order they met: those with it and those
        /// not (<see cref="NotFollowing"/>). None for a plan still open or void.
        /// </summary>
        public static List<string> Voted(EpisodeState s, PactPlanRow row)
        {
            if (s == null || row?.present == null || string.IsNullOrEmpty(row.targetId)) return new List<string>();
            var followed = row.followed ?? new List<string>();
            var not = NotFollowing(s, row);
            return row.present.Where(id => id != s.playerId && (followed.Contains(id) || not.Contains(id))).Distinct().ToList();
        }

        /// <summary>
        /// Who of a settled plan's voters the player was told is with it: where the player called it, everybody
        /// with it, as the answer's line named them ("Riley Chen and Sam Ortiz are with you"); where an NPC leads
        /// it, those who ended on its target, as its line said ("as Riley Chen wanted", "held to") - a dissenter
        /// who went along on their own coin was never said, so is not here.
        /// </summary>
        public static List<string> ToldWith(EpisodeState s, PactPlanRow row)
        {
            var with = Voted(s, row).Where(id => row.followed != null && row.followed.Contains(id));
            return (PlayerCalled(s, row) ? with : with.Where(id => FinalSay(row, id) == row.targetId)).ToList();
        }

        // ------------------------------------------------------------ the words

        /// <summary>
        /// The says, in words and never a number: "Riley Chen and Sam Ortiz want Maya Hassan out; Jo Park
        /// wants Alex Moore out; Kim Lee kept quiet." Grouped by whom they named, in the order the members
        /// met; the quiet last.
        /// </summary>
        public static string SaysSentence(EpisodeState s, IList<string> at, IList<PlanSay> says)
        {
            var parts = new List<string>();
            foreach (var group in says.Where(say => say != null).GroupBy(say => say.targetId))
            {
                var who = group.Select(say => Name(s, say.memberId)).ToList();
                parts.Add(Allegiance.Join(who) + (who.Count == 1 ? " wants " : " want ") + Name(s, group.Key) + " out");
            }
            var quiet = at.Where(id => says.All(say => say?.memberId != id)).Select(id => Name(s, id)).ToList();
            if (quiet.Count > 0) parts.Add(Allegiance.Join(quiet) + " kept quiet");
            return string.Join("; ", parts) + ".";
        }

        /// <summary>
        /// A war room's line (kind conversation): the meeting's own words (<see cref="EpisodeEngine.MeetingLine"/>)
        /// and the says - "The Riley Pact met where nobody listens: you, Riley Chen and Sam Ortiz went over
        /// the plan. Riley Chen wants Maya Hassan out; Sam Ortiz kept quiet."
        /// </summary>
        public static string WarRoomLine(EpisodeState s, AllianceState pact, IList<string> at, IList<PlanSay> says) =>
            EpisodeEngine.MeetingLine(s, pact, at, null, null) + " " + SaysSentence(s, at, says);

        /// <summary>
        /// How a plan settled, in one line to the pact (kind pact-plan). Going with it: "You went with The
        /// Riley Pact's plan: evict Maya Hassan. Riley Chen and Sam Ortiz are with you." A counter: "You pushed
        /// for Alex Moore. Sam Ortiz came round; Riley Chen held to Maya Hassan. The Riley Pact goes with Alex
        /// Moore." - with who is with the player where the counter carried. Lying low, and the campaign's
        /// close: "You let The Riley Pact's plan stand: evict Maya Hassan, as Riley Chen wanted." Void:
        /// "The Riley Pact's plan came to nothing."
        /// </summary>
        public static string SettledLine(EpisodeState s, AllianceState pact, string pactName, Settlement settled, string chosenId)
        {
            string name = pact?.name ?? pactName ?? "The alliance";
            if (settled.Void) return name + "'s plan came to nothing.";
            string target = Name(s, settled.targetId);
            if (settled.stance == PactPlanStance.Agreed)
                return "You went with " + name + "'s plan: evict " + target + "." + WithYou(s, settled);
            if (settled.stance == PactPlanStance.Countered)
            {
                var held = settled.finalSays.Where(say => say.targetId == settled.membersPlanId).Select(say => Name(s, say.memberId)).ToList();
                var came = settled.cameRound.Select(id => Name(s, id)).ToList();
                var parts = new List<string>();
                if (came.Count > 0) parts.Add(Allegiance.Join(came) + " came round");
                if (held.Count > 0) parts.Add(Allegiance.Join(held) + " held to " + Name(s, settled.membersPlanId));
                string line = "You pushed for " + Name(s, chosenId) + "." + (parts.Count > 0 ? " " + string.Join("; ", parts) + "." : "")
                    + " " + name + (settled.targetId == s.playerId ? " goes with its plan to evict you." : " goes with " + target + ".");
                return settled.callerId == s.playerId ? line + WithYou(s, settled) : line;
            }
            var wanted = settled.finalSays.Where(say => say.targetId == settled.targetId).Select(say => Name(s, say.memberId)).ToList();
            string because = wanted.Count > 0 ? ", as " + Allegiance.Join(wanted) + " wanted" : "";
            // The player on the block as the plan: "You let The Riley Pact's plan to evict you stand, as Riley Chen wanted."
            if (settled.targetId == s.playerId) return "You let " + name + "'s plan to evict you stand" + because + ".";
            return "You let " + name + "'s plan stand: evict " + target + because + ".";
        }

        /// <summary>" Riley Chen and Sam Ortiz are with you; Jo Park isn't." - the members who vote, as a call says it.</summary>
        private static string WithYou(EpisodeState s, Settlement settled)
        {
            if (settled.offered.Count == 0) return "";
            var with = settled.offered.Where(settled.followed.Contains).Select(id => Name(s, id)).ToList();
            var not = settled.offered.Where(id => !settled.followed.Contains(id)).Select(id => Name(s, id)).ToList();
            string line = " " + (with.Count == 0 ? "Nobody is with you" : Allegiance.Join(with) + (with.Count == 1 ? " is" : " are") + " with you");
            return line + (not.Count == 0 ? "." : "; " + Allegiance.Join(not) + (not.Count == 1 ? " isn't." : " aren't."));
        }

        /// <summary>A plan card's heading: "THE RILEY PACT'S PLAN".</summary>
        public static string Heading(string pactName) => (pactName ?? "").ToUpperInvariant() + "'S PLAN";

        /// <summary>One line of a plan card: a member, what they said, and the player's read of their vote.</summary>
        public sealed class CardLine
        {
            public string memberId, text;
            /// <summary>The player's whip word for the member's vote (<see cref="VoteRead.Firm"/> and its kin), or null for one who does not vote.</summary>
            public string whip;
        }

        /// <summary>
        /// What an open plan's card shows: each member still here and what they said - "Riley Chen wants
        /// Maya Hassan out", "Kim Lee kept quiet" - and, for each who votes, the player's own read of their
        /// vote in a word (<see cref="VoteRead.ReadVoter"/> on the season as the player knows it): firm,
        /// leaning, torn or unknown. Words only, never a number.
        /// </summary>
        public static List<CardLine> CardFacts(EpisodeState s, AllianceState pact, PactPlanRow row)
        {
            var lines = new List<CardLine>();
            if (s == null || pact == null || row == null) return lines;
            var standing = StandingSays(s, pact, row);
            var known = Allegiance.AsThePlayerKnows(s);
            bool read = VoteRead.Available(s);
            var voters = new HashSet<string>(EpisodeEngine.Voters(s).Where(v => !v.isPlayer).Select(v => v.id));
            foreach (string id in StillHere(s, pact, row))
            {
                string said = standing.FirstOrDefault(say => say.memberId == id)?.targetId;
                lines.Add(new CardLine
                {
                    memberId = id,
                    text = Name(s, id) + (said != null ? " wants " + Name(s, said) + " out" : " kept quiet"),
                    whip = read && voters.Contains(id) ? VoteRead.ReadVoter(known, id, EpisodeEngine.ProjectBallot(known, id)).confidence : null,
                });
            }
            return lines;
        }

        /// <summary>A card line in words: "Riley Chen wants Maya Hassan out · leaning".</summary>
        public static string CardText(CardLine line) => line.whip == null ? line.text : line.text + " · " + line.whip;

        /// <summary>A houseguest's name, "you" for the player.</summary>
        private static string Name(EpisodeState s, string id) => id == s.playerId ? "you" : s.Find(id)?.name ?? "somebody";
    }
}
