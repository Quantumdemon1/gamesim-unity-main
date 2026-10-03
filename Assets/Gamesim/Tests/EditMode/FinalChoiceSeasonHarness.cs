#if !UNITY_5_3_OR_NEWER
// The final Head of Household's choice over sampled seasons (ACTIONS-DEALS-ALLIANCES-PLAN C9): Tools/
// SimulationTests only. Unity's batch runner executes [Explicit] tests, so the file is compiled out of
// the editor's assemblies.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Gamesim.Simulation;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// How a houseguest who holds the final Head of Household chooses, when the player is one of the
    /// two they choose between (ACTIONS-DEALS-ALLIANCES-PLAN 1.4, C9). Plays 1,500 seeded seasons under
    /// the director's rule set - houses of 5, 6, 8, 10 and 12, a hundred seeds each, and three scripted
    /// players: one who only does what each phase asks (nominating in cast order, voting by a coin), a
    /// busy one, and one who builds pacts and endgame deals with the three houseguests warmest to them,
    /// both of whom nominate and vote by their own reads - to the final eviction, and reads the choice
    /// there exactly as the engine makes it (<see cref="EpisodeEngine.FinalChoice"/>): every term for
    /// each finalist, which terms decided it, the Head of Household's pacts and deals with each, and how
    /// the jury would vote between the Head of Household and either finalist.
    ///
    /// <para>Explicit: run with <c>dotnet test Tools/SimulationTests --filter "FullyQualifiedName~FinalChoiceSeasonHarness"</c>.
    /// The distribution is printed; the asserts hold what C9 is about.</para>
    /// </summary>
    public sealed class FinalChoiceSeasonHarness
    {
        private static readonly int[] Sizes = { 5, 6, 8, 10, 12 };
        private static readonly string[] Policies = { "passive", "busy", "allied" };
        private const uint Seeds = 100;

        /// <summary>One final eviction a houseguest decided with the player among the two they chose between.</summary>
        private sealed class Case
        {
            public string policy, hohName, otherName;
            public int size;
            public uint seed;
            public bool playerEvicted, engineAgreed;
            public double margin, playerScore, otherScore;
            public Dictionary<string, double> player = new Dictionary<string, double>(), other = new Dictionary<string, double>();
            public bool pactWithPlayer, pactHoldsWithPlayer, pactWithOther, finalTwoWithPlayer, finalTwoWithOther, promiseToPlayer;
            public double viewOfPlayer, viewOfOther, playerViewOfHoh, otherViewOfPlayer, jurorsViewOfPlayer, jurorsViewOfOther, jurorsViewOfHoh;
            public bool bondWithOther, bondWithPlayer, grudgeOnPlayer, grudgeOnOther;
            public int playerWins, otherWins, hohWins, jurors, playerNominatedHoh;
            /// <summary>The jury's expected votes for the Head of Household beside the player, and beside the other finalist.</summary>
            public double juryBesidePlayer, juryBesideOther;
            /// <summary>The Head of Household's chance to win the jury's vote beside the player, and beside the other finalist.</summary>
            public double winBesidePlayer, winBesideOther;
        }

        /// <summary>What a season came to: the player out before the final three, the player the final Head of Household, or a case.</summary>
        private sealed class Outcome
        {
            public string policy;
            public int size;
            public uint seed;
            public string ending;
            public Case finalChoice;
            public string error;
        }

        [Test, Explicit("Plays 1,500 seasons to the final eviction: the final Head of Household's choice without the commitment rules.")]
        public void WithoutTheRules()
        {
            var outcomes = Sample(false);
            Report("Without the commitment rules", outcomes);
            Assert.That(outcomes.Where(o => o.error != null).Select(o => o.error), Is.Empty, "Every season reached its final eviction.");
            Assert.That(outcomes.Where(o => o.finalChoice != null).All(o => o.finalChoice.engineAgreed), Is.True,
                "The engine evicted whom the choice read here selected.");
            // The investigation's finding, as 6fc74b8 plays it: a season without the commitment rules evicts the
            // player in 214 of these 228 final threes. A season without the rules must play as it did, so this never moves.
            var cases = outcomes.Where(o => o.finalChoice != null).Select(o => o.finalChoice).ToList();
            Assert.That((cases.Count(c => c.playerEvicted), cases.Count), Is.EqualTo((214, 228)), "A season without the commitment rules moved.");
        }

        [Test, Explicit("Plays 1,500 seasons to the final eviction: the final Head of Household's choice under the commitment rules.")]
        public void UnderTheRules()
        {
            var outcomes = Sample(true);
            Report("Under the commitment rules", outcomes);
            Assert.That(outcomes.Where(o => o.error != null).Select(o => o.error), Is.Empty, "Every season reached its final eviction.");
            Assert.That(outcomes.Where(o => o.finalChoice != null).All(o => o.finalChoice.engineAgreed), Is.True,
                "The engine evicted whom the choice read here selected.");
        }

        // ---------------------------------------------------------------- the sample

        private static List<Outcome> Sample(bool rulesOn)
        {
            var jobs = new List<(string policy, int size, uint seed)>();
            foreach (var policy in Policies)
            foreach (int size in Sizes)
            for (uint seed = 1; seed <= Seeds; seed++)
                jobs.Add((policy, size, seed));
            // The story catalogue builds itself once, lazily; build it before the seasons run side by side.
            Assert.That(StoryCatalog.All.Count, Is.GreaterThan(0));
            var outcomes = new Outcome[jobs.Count];
            Parallel.For(0, jobs.Count, i => outcomes[i] = Play(jobs[i].policy, jobs[i].size, jobs[i].seed, rulesOn));
            return outcomes.ToList();
        }

        private static EpisodeState Season(int size, uint seed, bool rulesOn)
        {
            // The director's rule set (EpisodeDirector.StartSeason), with the commitment rules from week one or not at all.
            var s = SeasonBuilder.Create(new SeasonBuilder.Choice { HouseSize = size }, seed);
            s.competitionRulesVersion = CompetitionRules.Current;
            s.haveNotRulesStartWeek = 1; s.strategyRulesStartWeek = 1;
            EpisodeEngine.EnableStory(s); EpisodeEngine.EnableRead(s); EpisodeEngine.EnableLevers(s);
            EpisodeEngine.EnableWeek(s); EpisodeEngine.EnableAgency(s); EpisodeEngine.EnableFinale(s);
            if (rulesOn) EpisodeEngine.EnableCommitments(s);
            return s;
        }

        private static Outcome Play(string policy, int size, uint seed, bool rulesOn)
        {
            var outcome = new Outcome { policy = policy, size = size, seed = seed };
            var engine = new EpisodeEngine(Season(size, seed, rulesOn));
            for (int i = 0; i < 4000; i++)
            {
                var s = engine.Snapshot;
                var me = s.Find(s.playerId);
                if (me.status != ContestantStatus.Active) { outcome.ending = "out"; return outcome; }
                if (s.phase == EpisodePhase.FinalEviction)
                {
                    if (s.hohId == s.playerId) { outcome.ending = "player-hoh"; return outcome; }
                    var read = Read(s);
                    read.policy = policy; read.size = size; read.seed = seed;
                    var after = engine.Apply(Cmd(s, EpisodeCommandKind.Advance, null));
                    if (!after.accepted) { outcome.error = "final eviction: " + after.reason; return outcome; }
                    string evicted = after.state.contestants.Single(c => c.status != ContestantStatus.Active
                        && s.Find(c.id).status == ContestantStatus.Active).id;
                    read.engineAgreed = (evicted == s.playerId) == read.playerEvicted;
                    outcome.ending = "case";
                    outcome.finalChoice = read;
                    return outcome;
                }
                bool done = false;
                for (int attempt = 0; attempt < 6 && !done; attempt++)
                {
                    var own = policy == "busy" ? Busy(s, seed, attempt) : policy == "allied" ? Allied(s, seed, attempt) : null;
                    if (own == null) break;
                    done = engine.Apply(own).accepted;
                }
                if (done) continue;
                var next = NextCommand(s, policy != "passive");
                var applied = engine.Apply(next);
                if (!applied.accepted) { outcome.error = policy + " n" + size + " s" + seed + " week " + s.week + " " + s.phase + " " + next.kind + ": " + applied.reason; return outcome; }
            }
            outcome.error = policy + " n" + size + " s" + seed + ": unfinished";
            return outcome;
        }

        // ---------------------------------------------------------------- reading the choice

        private static Case Read(EpisodeState s)
        {
            var evaluation = EpisodeEngine.FinalChoice(s);
            var hoh = s.Find(s.hohId);
            var other = s.Active.Single(c => c.id != s.hohId && c.id != s.playerId);
            var me = s.Find(s.playerId);
            var c = new Case
            {
                hohName = hoh.name, otherName = other.name,
                playerEvicted = evaluation.selectedNomineeId == s.playerId,
                margin = evaluation.margin,
                pactWithPlayer = s.Allied(hoh.id, s.playerId),
                pactHoldsWithPlayer = Allegiance.Holds(s, hoh.id, s.playerId),
                pactWithOther = s.Allied(hoh.id, other.id),
                finalTwoWithPlayer = FinalTwoDeal(s, hoh.id, s.playerId),
                finalTwoWithOther = FinalTwoDeal(s, hoh.id, other.id),
                promiseToPlayer = s.promises.Any(p => p.status == PromiseStatus.Active && p.kind == PromiseKind.FinalTwo && p.fromId == hoh.id && p.toId == s.playerId),
                viewOfPlayer = s.Score(hoh.id, s.playerId), viewOfOther = s.Score(hoh.id, other.id), playerViewOfHoh = s.Score(s.playerId, hoh.id),
                playerWins = me.hohWins + me.vetoWins, otherWins = other.hohWins + other.vetoWins, hohWins = hoh.hohWins + hoh.vetoWins,
            };
            foreach (var nominee in evaluation.nomineeEvaluations)
            {
                var into = nominee.nomineeId == s.playerId ? c.player : c.other;
                foreach (var factor in nominee.factors)
                {
                    into.TryGetValue(factor.code, out double sum);
                    into[factor.code] = sum + factor.value;
                }
                if (nominee.nomineeId == s.playerId) c.playerScore = nominee.score; else c.otherScore = nominee.score;
            }
            var jurors = s.contestants.Where(x => x.status == ContestantStatus.Jury || x.status == ContestantStatus.Evicted).Select(x => x.id).ToList();
            c.jurors = jurors.Count + 1;
            c.otherViewOfPlayer = s.Score(other.id, s.playerId);
            c.jurorsViewOfPlayer = jurors.Count == 0 ? 0 : jurors.Average(j => s.Score(j, s.playerId));
            c.jurorsViewOfOther = jurors.Count == 0 ? 0 : jurors.Average(j => s.Score(j, other.id));
            c.jurorsViewOfHoh = jurors.Count == 0 ? 0 : jurors.Average(j => s.Score(j, hoh.id));
            c.bondWithOther = StoryConsumers.VoteBond(s, hoh.id, other.id) > 0;
            c.bondWithPlayer = StoryConsumers.VoteBond(s, hoh.id, s.playerId) > 0;
            c.grudgeOnPlayer = StoryConsumers.VoteGrudge(s, hoh.id, s.playerId) < 0;
            c.grudgeOnOther = StoryConsumers.VoteGrudge(s, hoh.id, other.id) < 0;
            c.playerNominatedHoh = s.ledger.power.Count(p => p.hohId == s.playerId && (p.nominees.Contains(hoh.id) || p.replacementId == hoh.id));
            c.juryBesidePlayer = ExpectedVotes(s, hoh.id, s.playerId, jurors.Append(other.id));
            c.juryBesideOther = ExpectedVotes(s, hoh.id, other.id, jurors.Append(s.playerId));
            c.winBesidePlayer = WinChance(s, hoh.id, s.playerId, jurors.Append(other.id).ToList());
            c.winBesideOther = WinChance(s, hoh.id, other.id, jurors.Append(s.playerId).ToList());
            return c;
        }

        private static bool FinalTwoDeal(EpisodeState s, string a, string b) =>
            s.deals.Any(d => d.status == DealStatus.Active && d.type == DealKind.FinalTwo
                             && ((d.proposerId == a && d.recipientId == b) || (d.proposerId == b && d.recipientId == a)));

        /// <summary>
        /// The jury's expected votes for <paramref name="hohId"/> against <paramref name="finalistId"/>: each juror
        /// scores both (<see cref="WebJuryVoting.Score"/>) and adds a final impression of up to ten either way to
        /// each, as the engine's jury does; the chance one score beats the other is the triangle the two draws make.
        /// </summary>
        private static double ExpectedVotes(EpisodeState s, string hohId, string finalistId, IEnumerable<string> jurors) =>
            jurors.Sum(j => BeatChance(WebJuryVoting.Score(s, j, hohId) - WebJuryVoting.Score(s, j, finalistId)));

        /// <summary>The chance those votes make a majority for the Head of Household, a tie going to the second finalist in cast order as the reveal awards it.</summary>
        private static double WinChance(EpisodeState s, string hohId, string finalistId, List<string> jurors)
        {
            var p = jurors.Select(j => BeatChance(WebJuryVoting.Score(s, j, hohId) - WebJuryVoting.Score(s, j, finalistId))).ToList();
            var dist = new double[p.Count + 1]; dist[0] = 1;
            foreach (double q in p)
                for (int k = dist.Length - 1; k >= 0; k--) dist[k] = dist[k] * (1 - q) + (k > 0 ? dist[k - 1] * q : 0);
            bool hohSecond = s.contestants.FindIndex(c => c.id == hohId) > s.contestants.FindIndex(c => c.id == finalistId);
            double win = 0;
            for (int k = 0; k < dist.Length; k++)
            {
                int against = p.Count - k;
                if (k > against || (k == against && hohSecond)) win += dist[k];
            }
            return win;
        }

        private static double BeatChance(double d)
        {
            double spread = 2 * WebJuryVoting.FinalImpression;
            if (d <= -spread) return 0;
            if (d >= spread) return 1;
            return d <= 0 ? (d + spread) * (d + spread) / (2 * spread * spread) : 1 - (spread - d) * (spread - d) / (2 * spread * spread);
        }

        // ---------------------------------------------------------------- the report

        private static void Report(string title, List<Outcome> outcomes)
        {
            var o = new StringBuilder();
            var cases = outcomes.Where(x => x.finalChoice != null).Select(x => x.finalChoice).ToList();
            o.AppendLine("== " + title + ": " + outcomes.Count + " seasons; the player out before the final three in "
                + outcomes.Count(x => x.ending == "out") + ", the final Head of Household in " + outcomes.Count(x => x.ending == "player-hoh")
                + ", and among the final three under a houseguest's choice in " + cases.Count + ".");
            o.AppendLine("The player evicted: " + Rate(cases, c => c.playerEvicted));
            foreach (var policy in Policies) o.AppendLine("  " + policy + ": " + Rate(cases.Where(c => c.policy == policy).ToList(), c => c.playerEvicted));
            foreach (int size in Sizes) o.AppendLine("  house of " + size + ": " + Rate(cases.Where(c => c.size == size).ToList(), c => c.playerEvicted));
            o.AppendLine("By what binds the Head of Household:");
            Split(o, cases, "a pact with the player that holds", c => c.pactHoldsWithPlayer);
            Split(o, cases, "a pact with the other finalist", c => c.pactWithOther);
            Split(o, cases, "a final two deal with the player", c => c.finalTwoWithPlayer);
            Split(o, cases, "a final two deal with the other finalist", c => c.finalTwoWithOther);
            Split(o, cases, "a final two promise to the player", c => c.promiseToPlayer);
            Split(o, cases, "a warmer view of the player than of the other", c => c.viewOfPlayer > c.viewOfOther);
            Split(o, cases, "more competition wins for the player than the other", c => c.playerWins > c.otherWins);
            Split(o, cases, "the jury would vote for the Head of Household beside the player more than beside the other", c => c.juryBesidePlayer > c.juryBesideOther);
            Split(o, cases, "the player would beat the Head of Household in front of the jury", c => c.juryBesidePlayer * 2 < c.jurors);
            Split(o, cases, "the Head of Household's chance to win the jury is better beside the player", c => c.winBesidePlayer > c.winBesideOther);
            Split(o, cases, "a story grudge the Head of Household holds against the player", c => c.grudgeOnPlayer);
            var codes = cases.SelectMany(c => c.player.Keys.Concat(c.other.Keys)).Distinct().OrderBy(k => k, StringComparer.Ordinal).ToList();
            o.AppendLine("Each term, the other finalist's less the player's (positive keeps the other); mean over every case, over the cases that evicted the player,");
            o.AppendLine("and the cases each term decided on its own (without it the choice goes the other way):");
            foreach (string code in codes)
            {
                double all = cases.Average(c => Term(c.other, code) - Term(c.player, code));
                var evicted = cases.Where(c => c.playerEvicted).ToList();
                double whenEvicted = evicted.Count == 0 ? 0 : evicted.Average(c => Term(c.other, code) - Term(c.player, code));
                int decided = cases.Count(c => Flips(c, code));
                int decidedAgainst = evicted.Count(c => Flips(c, code));
                o.AppendLine("  " + code.PadRight(15) + " mean " + F(all).PadLeft(7) + "   evicted " + F(whenEvicted).PadLeft(7)
                    + "   decided " + decided + " (" + decidedAgainst + " of them evicting the player)");
            }
            o.AppendLine("The player's mean terms, and the other finalist's:");
            foreach (string code in codes)
                o.AppendLine("  " + code.PadRight(15) + F(cases.Average(c => Term(c.player, code))).PadLeft(7) + F(cases.Average(c => Term(c.other, code))).PadLeft(9));
            o.AppendLine("Mean margin " + F(cases.Average(c => c.margin)) + "; mean view of the player " + F(cases.Average(c => c.viewOfPlayer))
                + ", of the other " + F(cases.Average(c => c.viewOfOther)) + "; mean wins, player " + F(cases.Average(c => (double)c.playerWins))
                + ", other " + F(cases.Average(c => (double)c.otherWins)) + ", Head of Household " + F(cases.Average(c => (double)c.hohWins)) + ".");
            o.AppendLine("The jury's expected votes for the Head of Household: beside the player " + F(cases.Average(c => c.juryBesidePlayer))
                + ", beside the other " + F(cases.Average(c => c.juryBesideOther)) + ", of " + F(cases.Average(c => (double)c.jurors)) + " jurors; their chance to win beside the player "
                + F(cases.Average(c => c.winBesidePlayer)) + ", beside the other " + F(cases.Average(c => c.winBesideOther)) + ".");
            foreach (var policy in Policies)
            {
                var mine = cases.Where(c => c.policy == policy).ToList();
                if (mine.Count == 0) continue;
                o.AppendLine("  " + policy + ": the Head of Household's view of the player " + F(mine.Average(c => c.viewOfPlayer)) + ", of the other " + F(mine.Average(c => c.viewOfOther))
                    + "; the other's view of the player " + F(mine.Average(c => c.otherViewOfPlayer)) + "; the jurors' of the player " + F(mine.Average(c => c.jurorsViewOfPlayer))
                    + ", of the other " + F(mine.Average(c => c.jurorsViewOfOther)) + ", of the Head of Household " + F(mine.Average(c => c.jurorsViewOfHoh))
                    + "; margin " + F(mine.Average(c => c.margin)));
            }
            o.AppendLine("A story bond between the Head of Household and the other: " + cases.Count(c => c.bondWithOther) + ", and the player: " + cases.Count(c => c.bondWithPlayer)
                + "; their grudge on the player: " + cases.Count(c => c.grudgeOnPlayer) + ", on the other: " + cases.Count(c => c.grudgeOnOther)
                + "; the player had nominated the Head of Household: " + cases.Count(c => c.playerNominatedHoh > 0) + ".");
            var margins = cases.Select(c => c.otherScore - c.playerScore).OrderBy(m => m).ToList();
            o.AppendLine("The other's lead over the player, quartiles: " + string.Join(" / ", new[] { 0.1, 0.25, 0.5, 0.75, 0.9 }.Select(q => F(margins[(int)Math.Min(margins.Count - 1, q * margins.Count)]))));
            o.AppendLine("The first twelve cases:");
            foreach (var c in cases.Take(12))
                o.AppendLine("  " + c.policy + " n" + c.size + " s" + c.seed + ": " + c.hohName + (c.playerEvicted ? " evicts the player" : " takes the player")
                    + " over " + c.otherName + " by " + F(c.margin) + "; player " + Terms(c.player) + " | other " + Terms(c.other));
            TestContext.Out.WriteLine(o.ToString());
        }

        private static void Split(StringBuilder o, List<Case> cases, string what, Func<Case, bool> test)
        {
            var with = cases.Where(test).ToList();
            var without = cases.Where(c => !test(c)).ToList();
            o.AppendLine("  " + what + ": " + Rate(with, c => c.playerEvicted) + "; without: " + Rate(without, c => c.playerEvicted));
        }

        private static string Rate(List<Case> cases, Func<Case, bool> test) =>
            cases.Count == 0 ? "no cases" : cases.Count(test) + " of " + cases.Count + " (" + (100.0 * cases.Count(test) / cases.Count).ToString("0", CultureInfo.InvariantCulture) + "%)";

        private static double Term(Dictionary<string, double> terms, string code) => terms.TryGetValue(code, out double v) ? v : 0;

        /// <summary>Whether the choice goes the other way without this term, for both finalists.</summary>
        private static bool Flips(Case c, string code)
        {
            double before = c.otherScore - c.playerScore;
            double after = before - (Term(c.other, code) - Term(c.player, code));
            return before != 0 && after != 0 && Math.Sign(before) != Math.Sign(after);
        }

        private static string Terms(Dictionary<string, double> terms) =>
            string.Join(" ", terms.Where(t => Math.Abs(t.Value) >= 0.05).OrderBy(t => t.Key, StringComparer.Ordinal).Select(t => t.Key + "=" + F(t.Value)));

        private static string F(double value) => value.ToString("0.0", CultureInfo.InvariantCulture);

        // ---------------------------------------------------------------- the players

        private static EpisodeCommand Cmd(EpisodeState s, EpisodeCommandKind kind, string target, string second = null, string text = null) =>
            new EpisodeCommand
            {
                id = "final-" + s.revision + "-" + kind + "-" + target + "-" + second + "-" + text, actorId = s.playerId, kind = kind,
                targetId = target, secondTargetId = second, text = text, expectedRevision = s.revision, expectedPhase = s.phase,
            };

        /// <summary>A coin that has nothing to do with the engine's stream.</summary>
        private static int Pick(EpisodeState s, uint seed, int salt, int n) =>
            n <= 0 ? 0 : (int)((((uint)s.revision * 2654435761u) ^ (seed * 40503u) ^ ((uint)salt * 2246822519u) ^ ((uint)s.week * 97u)) % 100003u) % n;

        private static bool ActionsLeft(EpisodeState s) => EpisodeEngine.SocialActionsSpent(s) < EpisodeEngine.SocialActionBudget(s);

        private static EpisodeCommand AnswerBeat(EpisodeState s)
        {
            var item = HouseEvents.Pending(s);
            if (item == null) return null;
            if (!item.IsStory)
                return item.choices.Count == 0 ? null : Cmd(s, EpisodeCommandKind.ResolveHouseEvent, item.id, null, item.choices[0].label);
            var choice = item.choices.FirstOrDefault(c => !c.lapse && !c.locked && !c.pickPerson && !c.conduct && (!c.costsAction || ActionsLeft(s)))
                ?? item.choices.FirstOrDefault(c => !c.locked && !c.pickPerson && !c.conduct && (!c.costsAction || ActionsLeft(s)))
                ?? item.choices.FirstOrDefault(c => c.lapse);
            return choice == null ? null : Cmd(s, EpisodeCommandKind.ProgressStoryline, item.id, choice.optionId);
        }

        /// <summary>A busy player: talk, promises, deals, pacts, lies and vents, aimed by <see cref="Pick"/>.</summary>
        private static EpisodeCommand Busy(EpisodeState s, uint seed, int attempt)
        {
            if (s.pendingDiary != null) return null;
            if (attempt == 0)
            {
                var beat = AnswerBeat(s);
                if (beat != null) return beat;
                var offer = NpcDeals.Pending(s).FirstOrDefault();
                if (offer != null) return Cmd(s, EpisodeCommandKind.RespondToDeal, offer.id, null, Pick(s, seed, 9, 3) == 0 ? "decline" : EpisodeEngine.AcceptDeal);
            }
            if ((s.phase != EpisodePhase.Social && s.phase != EpisodePhase.Campaign) || !ActionsLeft(s)) return null;
            var npcs = s.Active.Where(c => !c.isPlayer).ToList();
            if (npcs.Count == 0) return null;
            var a = npcs[Pick(s, seed, 1 + attempt * 7, npcs.Count)];
            var others = npcs.Where(x => x.id != a.id).ToList();
            var b = others.Count > 0 ? others[Pick(s, seed, 2 + attempt * 7, others.Count)] : null;
            switch (Pick(s, seed, 3 + attempt * 7, 12))
            {
                case 0: return Cmd(s, EpisodeCommandKind.PromiseSafety, a.id);
                case 1: return Cmd(s, EpisodeCommandKind.PromiseFinalTwo, a.id);
                case 2:
                case 3:
                {
                    var kinds = PlayerDeals.Available(s, a.id);
                    if (kinds.Count == 0) return null;
                    string kind = kinds[Pick(s, seed, 6 + attempt, kinds.Count)];
                    string about = kind == DealKind.TargetAgreement ? PlayerDeals.Subjects(s, a.id).FirstOrDefault()
                        : kind == DealKind.VoteSave || kind == DealKind.VoteEvict ? s.nominees.FirstOrDefault(id => id != a.id && id != s.playerId) : null;
                    return Cmd(s, EpisodeCommandKind.ProposeDeal, a.id, about, kind);
                }
                case 4: return Cmd(s, EpisodeCommandKind.FormAlliance, a.id);
                case 5: return Cmd(s, EpisodeCommandKind.SmallTalk, a.id);
                case 6: return Cmd(s, EpisodeCommandKind.Talk, a.id);
                case 7: return Cmd(s, EpisodeCommandKind.DiscussGame, a.id);
                case 8: return b != null ? Cmd(s, EpisodeCommandKind.SpreadLie, a.id, b.id) : null;
                case 9: return b != null ? Cmd(s, EpisodeCommandKind.VentAbout, a.id, b.id) : null;
                case 10: return Cmd(s, EpisodeCommandKind.HouseMeeting, null, null, EpisodeEngine.RallyTroops);
                default: return Cmd(s, EpisodeCommandKind.AskForIntel, a.id);
            }
        }

        /// <summary>
        /// A player who builds an endgame with the three houseguests warmest to them (a harness may peek):
        /// accepts what they offer, forms a pact with each (three at most under the commitment rules), strikes
        /// the endgame's deals with them as soon as they can be put, and otherwise spends the window on them.
        /// </summary>
        private static EpisodeCommand Allied(EpisodeState s, uint seed, int attempt)
        {
            if (s.pendingDiary != null) return null;
            if (attempt == 0)
            {
                var beat = AnswerBeat(s);
                if (beat != null) return beat;
                var offer = NpcDeals.Pending(s).FirstOrDefault();
                if (offer != null) return Cmd(s, EpisodeCommandKind.RespondToDeal, offer.id, null, EpisodeEngine.AcceptDeal);
            }
            if ((s.phase != EpisodePhase.Social && s.phase != EpisodePhase.Campaign) || !ActionsLeft(s)) return null;
            var closest = s.Active.Where(c => !c.isPlayer).OrderByDescending(c => s.Score(c.id, s.playerId)).ThenBy(c => c.id, StringComparer.Ordinal).Take(3).ToList();
            if (closest.Count == 0) return null;
            var a = closest[(attempt + Pick(s, seed, 4, closest.Count)) % closest.Count];
            var available = PlayerDeals.Available(s, a.id);
            int pacts = s.alliances.Count(p => p.active && p.members.Contains(s.playerId));
            if (attempt <= 1)
            {
                if (available.Contains(DealKind.FinalTwo)) return Cmd(s, EpisodeCommandKind.ProposeDeal, a.id, null, DealKind.FinalTwo);
                if (available.Contains("final_three")) return Cmd(s, EpisodeCommandKind.ProposeDeal, a.id, null, "final_three");
                if (!s.Allied(s.playerId, a.id) && pacts < 3) return Cmd(s, EpisodeCommandKind.FormAlliance, a.id);
            }
            if (attempt == 2 && available.Contains(DealKind.SafetyAgreement)) return Cmd(s, EpisodeCommandKind.ProposeDeal, a.id, null, DealKind.SafetyAgreement);
            return Cmd(s, Pick(s, seed, 5, 2) == 0 ? EpisodeCommandKind.Talk : EpisodeCommandKind.SmallTalk, a.id);
        }

        /// <summary>
        /// Whatever the phase asks of the player, the plainest legal answer (the digest harness's). A player who
        /// <paramref name="reads"/> the house nominates the two they think least of, their pact-mates last, and
        /// votes out the nominee they think less of; the plain answer nominates in cast order and votes by a coin.
        /// </summary>
        private static EpisodeCommand NextCommand(EpisodeState s, bool reads)
        {
            var c = Cmd(s, EpisodeCommandKind.Advance, null);
            c.id = "next-" + s.revision;
            if (s.pendingDiary != null) { c.kind = EpisodeCommandKind.SkipDiary; c.targetId = s.pendingDiary.id; }
            else if (EpisodeEngine.IsCompetition(s.phase) && !s.competitionResolved && EpisodeEngine.CompetitionPlayers(s).Any(p => p.isPlayer))
            { c.kind = EpisodeCommandKind.Compete; c.performance = 0.5; }
            else if (s.phase == EpisodePhase.Nomination && s.nominees.Count == 0 && s.hohId == s.playerId)
            {
                c.kind = EpisodeCommandKind.Nominate;
                var pool = EpisodeEngine.NominationCandidates(s).ToArray();
                if (reads) pool = pool.OrderBy(x => s.Allied(s.playerId, x.id) ? 1 : 0).ThenBy(x => s.Score(s.playerId, x.id)).ThenBy(x => x.id, StringComparer.Ordinal).ToArray();
                c.targetId = pool[0].id; c.secondTargetId = pool[1].id;
            }
            else if (s.phase == EpisodePhase.VetoMeeting && !s.vetoResolved && (s.vetoHolderId == s.playerId || (s.hohId == s.playerId && EpisodeEngine.NpcVetoSave(s) != null)))
            {
                c.kind = EpisodeCommandKind.ResolveVeto;
                c.useVeto = EpisodeEngine.ReplacementCandidates(s).Any() && !EpisodeEngine.VetoIsLockedAtFinalFour(s);
                c.targetId = s.vetoHolderId == s.playerId ? s.nominees[0] : EpisodeEngine.NpcVetoSave(s);
                c.secondTargetId = EpisodeEngine.ReplacementCandidates(s).FirstOrDefault()?.id;
            }
            else if (s.phase == EpisodePhase.Eviction && s.evictionStage == EvictionStage.Speeches
                && s.nominees.Contains(s.playerId) && !s.evictionSpeeches.Any(x => x.speakerId == s.playerId))
            { c.kind = EpisodeCommandKind.SubmitEvictionSpeech; c.text = "I'd like to stay."; }
            else if (s.phase == EpisodePhase.Eviction && !s.evictionResolved
                && (s.evictionStage == EvictionStage.Voting || s.evictionStage == EvictionStage.Tiebreaker)
                && !s.votes.Any(v => v.voterId == s.playerId) &&
                (EpisodeEngine.Voters(s).Any(v => v.isPlayer) || EpisodeEngine.NeedsPlayerTieBreak(s)))
            {
                c.kind = EpisodeCommandKind.CastVote;
                c.targetId = reads ? s.nominees.OrderBy(id => s.Allied(s.playerId, id) ? 1 : 0).ThenBy(id => s.Score(s.playerId, id)).ThenBy(id => id, StringComparer.Ordinal).First()
                    : s.nominees[(s.revision + s.week) % 2];
            }
            return c;
        }
    }
}
#endif
