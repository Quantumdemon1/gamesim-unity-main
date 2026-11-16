using System;
using System.Linq;

namespace Gamesim.Simulation
{
    /// <summary>
    /// Whether a houseguest tells the player the truth (plan 32 §J, "Candour"): Honest, Neutral or Dishonest, from their faith
    /// in the player (<see cref="Regard"/>), what the truth would cost them (the stake) and their traits, on a keyed coin of the
    /// story's own stream, so a reload never re-rolls it and the season's main stream is never touched.
    ///
    /// <para>Read from story rules 12 (<see cref="StoryRules.Manners"/>) by the vote answer. Weights are held in whole
    /// hundredths, so the three always sum to exactly one. The stake conditions §F adds at 14 land with the house's events
    /// (S8), checked only under <c>StoryAt(s, StoryRules.ComesToYou)</c>, so they never change a 12 or 13 answer.</para>
    /// </summary>
    public static class Candour
    {
        public const string Honest = "honest", Neutral = "neutral", Dishonest = "dishonest";
        public static readonly string[] Stances = { Honest, Neutral, Dishonest };
        public static bool IsStance(string stance) => stance != null && Array.IndexOf(Stances, stance) >= 0;

        /// <summary>The questions a stance answers (§J, "Question keys").</summary>
        public const string Vote = "vote", Intel = "intel", Rumour = "rumour", Pitch = "pitch", Talk = "talk", Approach = "approach", Where = "where";

        /// <summary>The three weights, in whole hundredths.</summary>
        public struct Weights
        {
            public int honest, neutral, dishonest;
            public Weights(int honest, int neutral, int dishonest) { this.honest = honest; this.neutral = neutral; this.dishonest = dishonest; }
            public double Honest => honest / 100.0;
            public double Neutral => neutral / 100.0;
            public double Dishonest => dishonest / 100.0;
            public override string ToString() => honest + "/" + neutral + "/" + dishonest;
        }

        public const int StakeMost = 2, StakeAtActive = 4, ShiftAtActive = 6;
        public const double StakeFear = 24, StakeLean = 15, HonestLean = 5;
        /// <summary>The prefix of the key the house's events write on a liar at the reveal (§F, at 14): it holds them at Neutral.</summary>
        public const string LiarPrefix = "liar:";

        /// <summary>The lean's bands, half-open from the bottom: lean ≥ 65, 45, 30, 15, and below.</summary>
        public static Weights Band(double lean) =>
            lean >= 65 ? new Weights(90, 10, 0)
            : lean >= 45 ? new Weights(60, 30, 10)
            : lean >= 30 ? new Weights(35, 40, 25)
            : lean >= 15 ? new Weights(20, 35, 45)
            : new Weights(10, 25, 65);

        /// <summary>
        /// The trait shifts, in order, each once and never more than its source holds (§J): Strategic, Analytical or
        /// Introverted move up to .10 honest to neutral; Sneaky, Deceptive or Manipulative up to .15 neutral to dishonest;
        /// Emotional or Impulsive up to .10 neutral to honest; at six or fewer active, Steady ≥ 2 up to .10 honest to neutral;
        /// a nemesis asked the vote all of honest to dishonest; Loyal all of dishonest to neutral.
        /// </summary>
        public static Weights Shift(Weights w, ContestantState npc, int active, bool nemesisOnTheVote)
        {
            if (npc == null) return w;
            var traits = npc.traits;
            bool Any(params string[] names) => names.Any(traits.Contains);
            if (Any("Strategic", "Analytical", "Introverted")) Move(ref w.honest, ref w.neutral, 10);
            if (Any("Sneaky", "Deceptive", "Manipulative")) Move(ref w.neutral, ref w.dishonest, 15);
            if (Any("Emotional", "Impulsive")) Move(ref w.neutral, ref w.honest, 10);
            if (active <= ShiftAtActive && Personality.Of(npc).Steady >= 2) Move(ref w.honest, ref w.neutral, 10);
            if (nemesisOnTheVote) Move(ref w.honest, ref w.dishonest, w.honest);
            if (traits.Contains("Loyal")) Move(ref w.dishonest, ref w.neutral, w.dishonest);
            return w;
        }

        private static void Move(ref int from, ref int to, int amount)
        {
            int moved = Math.Min(amount, from);
            from -= moved; to += moved;
        }

        /// <summary>
        /// The fixed values, the first matching row winning (§J): a C6 ally is Honest; a houseguest under a cold shoulder or
        /// a live <c>liar:</c> key is Neutral; a nemesis has none; a confidant is Honest. Null where the coin decides. The cold
        /// shoulder is the acquaintance thread's reader (S5, at 14) and joins here with it.
        /// </summary>
        public static string Fixed(EpisodeState s, string npcId)
        {
            if (s == null || string.IsNullOrEmpty(npcId)) return null;
            if (EpisodeEngine.SharesIntel(s, npcId)) return Honest;
            if (s.story?.cooldowns != null && s.story.cooldowns.Any(c => c.key == LiarPrefix + npcId && c.untilWeek > s.week)) return Neutral;
            if (Bonds.Nemesis(s, npcId, s.playerId)) return null;
            if (Bonds.Holds(s, npcId, s.playerId, BondKinds.Confidant)) return Honest;
            return null;
        }

        /// <summary>
        /// Who the houseguest is after now: their projected ballot while there is a vote they cast, otherwise the top of their
        /// ranked targets. Read, never stored.
        /// </summary>
        public static string TargetOf(EpisodeState s, string npcId)
        {
            if (VoteRead.Available(s) && EpisodeEngine.Voters(s).Any(v => v.id == npcId))
                return EpisodeEngine.ProjectBallot(s, npcId).selectedNomineeId;
            return ThreatAssessment.RankedTargets(s, npcId).FirstOrDefault();
        }

        /// <summary>
        /// What the truth costs them, 0 to 2 (§J): 2 when their target is the player or four or fewer are left; otherwise +1
        /// when the truth helps the player against them, +1 for fear ≥ 24 when they are not the player's ally, and +1 when
        /// the player holds the HoH or the veto and they are not the player's ally.
        /// </summary>
        public static int Stake(EpisodeState s, string npcId, string question, string subjectId)
        {
            string me = s.playerId;
            string target = TargetOf(s, npcId);
            if (target == me || s.Active.Count() <= StakeAtActive) return StakeMost;
            bool ally = s.Allied(npcId, me);
            int stake = 0;
            bool helps = false;
            if (target != null)
            {
                helps |= s.alliances.Any(a => a.active && a.members.Contains(me) && a.members.Contains(target) && Knowledge.AllianceVisibleTo(s, a, npcId));
                helps |= Bonds.Partner(s, me, target);
            }
            if (question == Intel && subjectId != null)
            {
                helps |= s.alliances.Any(a => a.active && a.members.Contains(npcId) && a.members.Contains(subjectId));
                helps |= s.nominees.Contains(subjectId) && s.hohId != null && s.Allied(npcId, s.hohId);
            }
            if (helps) stake++;
            if (!ally && Regard.Of(s, npcId).fear >= StakeFear) stake++;
            if (!ally && (s.hohId == me || s.vetoHolderId == me)) stake++;
            return Math.Min(StakeMost, stake);
        }

        /// <summary>The weights the coin is drawn on: the band of their lean, then the trait shifts. Exposed for tests.</summary>
        public static Weights WeightsFor(EpisodeState s, string npcId, string question, string subjectId)
        {
            var npc = s.Find(npcId);
            double lean = Regard.Of(s, npcId).faith - StakeLean * Stake(s, npcId, question, subjectId) + HonestLean * Personality.Of(npc).Honest;
            bool nemesisOnTheVote = question == Vote && Bonds.Nemesis(s, npcId, s.playerId);
            return Shift(Band(lean), npc, s.Active.Count(), nemesisOnTheVote);
        }

        /// <summary>The coin's key: <c>w{week}:candour:{question}:{npc}:{subject or '-'}</c>.</summary>
        public static string Key(EpisodeState s, string npcId, string question, string subjectId) =>
            "w" + s.week + ":candour:" + question + ":" + npcId + ":" + (subjectId ?? "-");

        /// <summary>The stance: a fixed value where one holds, otherwise the keyed coin on the weights. Draws nothing from the main stream.</summary>
        public static string Choose(EpisodeState s, string npcId, string question, string subjectId)
        {
            string fixedStance = Fixed(s, npcId);
            if (fixedStance != null) return fixedStance;
            var w = WeightsFor(s, npcId, question, subjectId);
            double u = StoryRandom.Unit(s, Key(s, npcId, question, subjectId));
            return u < w.Honest ? Honest : u < w.Honest + w.Neutral ? Neutral : Dishonest;
        }
    }

    /// <summary>
    /// How an answer is said (plan 32 §G2): plainly, or with a tell. A liar shows a tell at t_L, an honest speaker a "false
    /// tell" at t_H; which tell is the speaker's dominant one most of the time, so a tell is evidence, never certainty. Two keyed
    /// coins of the story's stream, never the main one.
    /// </summary>
    public static class Manner
    {
        public const string Definite = "definite", Hedged = "hedged", TurnedBack = "turned-back", LaidOnThick = "laid-on-thick";
        public const string NonAnswer = "non-answer", Deflect = "deflect";
        public static readonly string[] All = { Definite, Hedged, TurnedBack, LaidOnThick, NonAnswer, Deflect };
        public static readonly string[] Tells = { Hedged, TurnedBack, LaidOnThick };
        public static bool IsKnown(string manner) => manner != null && Array.IndexOf(All, manner) >= 0;
        public static bool IsTell(string manner) => manner != null && Array.IndexOf(Tells, manner) >= 0;

        public const double DominantShare = 0.70;

        /// <summary>t_L = clamp(0.50 + 0.08·Honest − 0.06·Steady + 0.10·[nominated], 0.15, 0.85).</summary>
        public static double LiarTell(ContestantState who, bool nominated)
        {
            var a = Personality.Of(who);
            return Clamp(0.50 + 0.08 * a.Honest - 0.06 * a.Steady + (nominated ? 0.10 : 0), 0.15, 0.85);
        }

        /// <summary>t_H = clamp(0.12 + 0.25·[thin margin] + 0.08·[Steady &lt; 0] − 0.05·[Steady ≥ 2] + 0.10·[nominated], 0.05, 0.45).</summary>
        public static double HonestTell(ContestantState who, bool thinMargin, bool nominated)
        {
            var a = Personality.Of(who);
            return Clamp(0.12 + (thinMargin ? 0.25 : 0) + (a.Steady < 0 ? 0.08 : 0) - (a.Steady >= 2 ? 0.05 : 0) + (nominated ? 0.10 : 0), 0.05, 0.45);
        }

        /// <summary>Bold ≥ 2: LaidOnThick; else Steady ≥ 2: TurnedBack; else Sociable ≥ 2: LaidOnThick; else Hedged.</summary>
        public static string Dominant(ContestantState who)
        {
            var a = Personality.Of(who);
            return a.Bold >= 2 ? LaidOnThick : a.Steady >= 2 ? TurnedBack : a.Sociable >= 2 ? LaidOnThick : Hedged;
        }

        /// <summary>The voice's one Neutral manner: Deflect for Blunt and Guarded, NonAnswer for Warm, Chatty and Plain.</summary>
        public static string NeutralFor(string voice) => voice == Voice.Blunt || voice == Voice.Guarded ? Deflect : NonAnswer;

        /// <summary>
        /// The manner of one answer. Neutral takes the voice's neutral manner and draws nothing. Otherwise the first coin
        /// (<c>w{w}:manner:{q}:{npc}</c>) decides whether a tell shows; a liar's tell is the dominant one 70% of the time and
        /// another 30%, on the second coin (<c>w{w}:manner-kind:{q}:{npc}</c>); an honest tell is Hedged on a thin margin and
        /// any of the three otherwise. No tell: Definite.
        /// </summary>
        public static string Choose(EpisodeState s, ContestantState who, string question, string stance, bool thinMargin, bool nominated)
        {
            if (stance == Candour.Neutral) return NeutralFor(Voice.Of(who, false));
            bool lying = stance == Candour.Dishonest;
            double t = lying ? LiarTell(who, nominated) : HonestTell(who, thinMargin, nominated);
            if (StoryRandom.Unit(s, "w" + s.week + ":manner:" + question + ":" + who.id) >= t) return Definite;
            double kind = StoryRandom.Unit(s, "w" + s.week + ":manner-kind:" + question + ":" + who.id);
            if (lying) return Pick(Dominant(who), kind);
            if (thinMargin) return Hedged;
            return Tells[Math.Min(Tells.Length - 1, (int)(kind * Tells.Length))];
        }

        /// <summary>The dominant tell below 0.70; above it, one of the other two, evenly.</summary>
        public static string Pick(string dominant, double kind)
        {
            if (kind < DominantShare) return dominant;
            var others = Tells.Where(t => t != dominant).ToArray();
            return others[Math.Min(others.Length - 1, (int)((kind - DominantShare) / (1 - DominantShare) * others.Length))];
        }

        private static double Clamp(double v, double lo, double hi) => Math.Max(lo, Math.Min(hi, v));
    }
}
