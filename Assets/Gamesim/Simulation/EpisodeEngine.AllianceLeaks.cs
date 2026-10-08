using System;
using System.Collections.Generic;
using System.Linq;

namespace Gamesim.Simulation
{
    /// <summary>
    /// Leaks and double-dealing (WAVE-D-NPC-PACTS-PLAN §2): the engine's half. The rules' numbers and
    /// words are <see cref="AllianceLeaks"/>'; here is where they act.
    /// </summary>
    public sealed partial class EpisodeEngine
    {
        /// <summary>Switches the leak rules on from a week, no later than the week after the season's own (schema 28's boundary).</summary>
        public static void EnableAllianceLeaks(EpisodeState s, int fromWeek = 1)
        {
            if (s == null) throw new ArgumentNullException(nameof(s));
            s.allianceLeakRulesStartWeek = Math.Max(1, Math.Min(fromWeek, s.week + 1));
        }

        // ---------------------------------------------------------------- the weekly leak

        /// <summary>
        /// The weekly leak, at the eviction night anchor under the leak rules: every pact that rolls this
        /// week (<see cref="AllianceLeaks.Rolls"/>), in the house's order, draws its keyed coin, and a pact
        /// whose coin lands is out as a whisper - its fact widened and nothing else: no knower, no line, no
        /// id. The house's gossip carries it from there, this anchor's first, and a whispered fact never
        /// rolls again. Each coin is the week's and the pact's alone, so the order changes nothing and a
        /// reload before the commit draws the same.
        /// </summary>
        private static void LeakPass(EpisodeState s)
        {
            foreach (var pact in s.alliances.ToList())
                if (AllianceLeaks.Leaks(s, pact))
                    Knowledge.MakeKnown(s, Knowledge.Of(s, FactKinds.Alliance, pact.id), FactVisibility.Whispered);
        }

        // ---------------------------------------------------------------- one pair, one pact

        /// <summary>
        /// The pact a story's alliance spread is about under the leak rules: the one pact of the two it
        /// names (<see cref="Knowledge.PactOfPair"/>), asked for its listener when it names one. Null for
        /// any other effect, or where no pact holds the two.
        /// </summary>
        private static AllianceState SpreadPact(EpisodeState s, StoryEffectState e)
        {
            if (e == null || e.kind != StoryEffects.Spread || e.type != FactKinds.Alliance) return null;
            if (s.Find(e.fromId) == null || s.Find(e.toId) == null) return null;
            return Knowledge.PactOfPair(s, e.fromId, e.toId, e.thirdId);
        }

        /// <summary>
        /// A story's alliance spread under the leak rules (WAVE-D-NPC-PACTS-PLAN §2.3, the C8 defect): it
        /// grants and widens exactly the one pact the pair names, where it once reached every pact holding
        /// both. A named listener learns of it and it is out as a whisper; with nobody named it goes as
        /// wide as the story says. The reach rules' skip stands: a pact with no fact is known to everyone
        /// already, so one more person hearing of it makes no secret of it.
        /// </summary>
        private static void SpreadOnePact(EpisodeState s, StoryEffectState e)
        {
            var alliance = SpreadPact(s, e);
            if (alliance == null) return;
            if (Knowledge.Of(s, FactKinds.Alliance, alliance.id) == null && e.thirdId != null && StoryAt(s, StoryRules.Reach)) return;
            if (Knowledge.Of(s, FactKinds.Alliance, alliance.id) == null) Knowledge.AllianceFormed(s, alliance);
            var fact = Knowledge.Of(s, FactKinds.Alliance, alliance.id);
            if (fact == null) return;
            var knewBefore = new List<string>(fact.knowers);
            if (e.thirdId != null)
            {
                Knowledge.AddKnower(s, fact, e.thirdId);
                Knowledge.MakeKnown(s, fact, FactVisibility.Whispered);
            }
            else Knowledge.MakeKnown(s, fact, FactVisibility.IsKnown(e.text) ? e.text : FactVisibility.Public);
            // Whoever the story told who did not know: an ally of the player's among them has caught the player out.
            foreach (string knower in fact.knowers.Where(id => !knewBefore.Contains(id)).ToList())
                CaughtDoubleDealing(s, fact, knower);
        }

        // ---------------------------------------------------------------- double-dealing

        /// <summary>
        /// Somebody has just come to know of a pact's fact - the house's gossip told them, or a story's
        /// spread did - under the leak rules (WAVE-D-NPC-PACTS-PLAN §2.3): where they share a pact with the
        /// player and this is another of the player's (<see cref="AllianceLeaks.IsCaughtOut"/>), they react.
        /// Knowers only grow, so it happens at most once for them and this pact, with no record kept.
        /// </summary>
        private static void CaughtDoubleDealing(EpisodeState s, HouseFactState fact, string knowerId)
        {
            if (fact == null || fact.kind != FactKinds.Alliance || !AllianceLeaks.On(s)) return;
            var pact = s.alliances.FirstOrDefault(a => a?.id == fact.refId);
            if (AllianceLeaks.IsCaughtOut(s, knowerId, pact)) DoubleDealt(s, knowerId, pact);
        }

        /// <summary>
        /// An ally who found out about the player's other pact. Deterministic, with no draw: one who has a
        /// rival in it (<see cref="AllianceLeaks.HasRival"/>) holds the alliance-betrayed grudge against the
        /// player - forty, the allied ×1.2 making it forty-eight, stacking at half - and anybody else keeps
        /// the permanent receipt (<see cref="StoryReceipts.DoubleDealt"/>, −10) on their view of the player,
        /// as talk about the player moves a listener (<see cref="HeardAbout"/>). Either way the same one
        /// line, to the player and to them (D4-M1). Nothing moves between two houseguests, nothing goes
        /// through <see cref="Change"/>, and the pact they share with the player is left alone.
        /// </summary>
        private static void DoubleDealt(EpisodeState s, string knowerId, AllianceState pact)
        {
            if (AllianceLeaks.HasRival(s, knowerId, pact))
                Grudges.Add(s, knowerId, s.playerId, AllianceLeaks.BetrayedGrudge, GrudgeCauses.AllianceBetrayed, alliedMultiplier: true);
            else HeardAbout(s, knowerId, AllianceLeaks.ReceiptImpact, AllianceLeaks.HeardNote(s, pact), StoryReceipts.DoubleDealt);
            Log(s, WaveDEventKinds.DoubleDealing, AllianceLeaks.Line(s, knowerId, pact), s.playerId, knowerId);
        }

        /// <summary>
        /// Before each of a beat's or a play's effects is applied, under the leak rules: the pact an
        /// alliance spread among them is about, kept so its receipt names the pact it granted - asked
        /// after the grant, a listener who now knows of it would be pointed at another.
        /// </summary>
        private static void RememberSpreadPact(EpisodeState s, StoryEffectState e, Dictionary<StoryEffectState, AllianceState> spread)
        {
            if (spread == null) return;
            var alliance = SpreadPact(s, e);
            if (alliance != null) spread[e] = alliance;
        }

        // ---------------------------------------------------------------- the listen-in

        /// <summary>
        /// The one line a listen-in that was not caught logs (X5, owned by D4; the shared builder D2 adds
        /// its act clause to), in its fixed clause order: what was overheard, "You overheard A and B. They
        /// {reading}."; the vote clause (<c>OverheardVote</c>); D2's act clause; and D4's sentence
        /// (<see cref="AllianceLeaks.ListenInSentence"/>). D2 passes no pact or meet clause where D4's
        /// sentence fires. Every clause null - every rule off - is the line the season always said, byte
        /// for byte. It adds no id and changes no audience: the caller logs it once, to the player.
        /// </summary>
        public static string EavesdropLine(string firstName, string secondName, string reading, string voteClause,
            string actClause, string pactSentence) =>
            "You overheard " + firstName + " and " + secondName + ". They " + reading + "." + voteClause + actClause + pactSentence;

        /// <summary>
        /// A listen-in that heard two people, under the leak rules (WAVE-D-NPC-PACTS-PLAN §2.3, D4-M2): the
        /// one pact of the two (<see cref="Knowledge.PactOfPair"/>, asked for the player) makes the player
        /// a knower of its fact only when it stands, has a fact, is not the player's own, is not known to
        /// them already, and the people of it in the house are exactly the two overheard - two members of a
        /// bigger pact give nothing away (§6 Q5). The fact stays private: overhearing starts no gossip, and
        /// the player's memory says only what it always said, so the pact cannot be passed on. Returns the
        /// sentence the line gains, or null when nothing was learned. No draw and no id.
        /// </summary>
        private static string ListenIn(EpisodeState s, ContestantState first, ContestantState second)
        {
            var pact = Knowledge.PactOfPair(s, first.id, second.id, s.playerId);
            if (pact == null || !pact.active || pact.members.Contains(s.playerId)) return null;
            var fact = Knowledge.Of(s, FactKinds.Alliance, pact.id);
            if (fact == null || Knowledge.Knows(fact, s.playerId)) return null;
            var here = pact.members.Where(id => s.Find(id)?.status == ContestantStatus.Active).Distinct().ToList();
            if (here.Count != 2 || !here.Contains(first.id) || !here.Contains(second.id)) return null;
            Knowledge.AddKnower(s, fact, s.playerId);
            return fact.knowers.Contains(s.playerId) ? AllianceLeaks.ListenInSentence(s, pact) : null;
        }
    }
}
