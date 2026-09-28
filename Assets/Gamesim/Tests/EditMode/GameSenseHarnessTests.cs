using System;
using System.Collections.Generic;
using System.Linq;
using Gamesim.Simulation;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// Proving skill matters (STRATEGY-LOOP-PLAN.md §7): two scripted players over the same seeded
    /// seasons. The reader gathers reads and acts on them (asks, reads, deals with the torn, pleads
    /// from the block, calls the vote in an alliance, votes with the whip count); the random player
    /// talks to whoever and votes on a coin. Game Sense must separate them, winners must spread, and
    /// the reader's win rate must not run away: the game is not solved by reading.
    /// </summary>
    public sealed class GameSenseHarnessTests
    {
        public sealed class Sweep
        {
            public string player;
            public int seasons, wins, finalTwos, errors;
            public string firstError;
            public double meanScore, meanCompetitions, meanStrategy, meanSocial, meanTaken, meanOffered;
            public List<int> scores = new List<int>();
            public List<int> winnerScores = new List<int>();
            public override string ToString() =>
                player + ": " + seasons + " seasons, Game Sense " + meanScore.ToString("0.0") + " (competitions " + meanCompetitions.ToString("0")
                + ", strategy " + meanStrategy.ToString("0") + ", social " + meanSocial.ToString("0") + "), won " + wins + ", final two " + finalTwos
                + ", chances " + meanTaken.ToString("0.0") + " of " + meanOffered.ToString("0.0") + " taken"
                + (winnerScores.Count > 0 ? ", winners scored " + winnerScores.Min() + "-" + winnerScores.Max() : "") + (errors > 0 ? ", errors " + errors : "");
        }

        /// <summary>An eight-house season with the story, the read and the levers on, as the director ships one.</summary>
        private static EpisodeState Season(uint seed)
        {
            var s = SeasonBuilder.Create(new SeasonBuilder.Choice { HouseSize = 8 }, seed);
            s.strategyRulesStartWeek = 1; s.blocRulesStartWeek = 1;
            EpisodeEngine.EnableStory(s); EpisodeEngine.EnableRead(s); EpisodeEngine.EnableLevers(s);
            return s;
        }

        private static EpisodeCommand Command(EpisodeState s, EpisodeCommandKind kind, string target = null, string second = null, string text = null) =>
            new EpisodeCommand
            {
                id = "sweep-" + s.revision + "-" + kind, actorId = s.playerId, kind = kind, targetId = target, secondTargetId = second, text = text,
                expectedRevision = s.revision, expectedPhase = s.phase,
            };

        /// <summary>A coin the random player tosses: deterministic per season and revision, nothing to do with the engine's stream.</summary>
        private static double Coin(EpisodeState s, uint seed, int salt) =>
            (((uint)s.revision * 2654435761u) ^ (seed * 40503u) ^ ((uint)salt * 2246822519u)) % 1000u / 1000.0;

        private static bool ActionsLeft(EpisodeState s) => EpisodeEngine.SocialActionsSpent(s) < EpisodeEngine.SocialActionBudget(s);

        /// <summary>A story beat pending: both players answer it with the first option that is not the lapse (the walk's rule), so the story does not separate them.</summary>
        private static EpisodeCommand AnswerBeat(EpisodeState s)
        {
            var item = HouseEvents.Pending(s);
            if (item == null || !item.IsStory) return null;
            bool actionsLeft = ActionsLeft(s);
            var choice = item.choices.FirstOrDefault(c => !c.lapse && !c.locked && !c.pickPerson && !c.conduct && (!c.costsAction || actionsLeft))
                ?? item.choices.FirstOrDefault(c => !c.locked && !c.pickPerson && !c.conduct && (!c.costsAction || actionsLeft))
                ?? item.choices.FirstOrDefault(c => c.lapse);
            return choice == null ? null : Command(s, EpisodeCommandKind.ProgressStoryline, item.id, choice.optionId);
        }

        private static ContestantState[] NpcVoters(EpisodeState s) => EpisodeEngine.Voters(s).Where(v => !v.isPlayer).ToArray();

        /// <summary>The reader: gathers reads and acts on them.</summary>
        private static EpisodeCommand ReaderNext(EpisodeState s, uint seed)
        {
            if (s.pendingDiary != null) return null;
            var beat = AnswerBeat(s);
            if (beat != null) return beat;
            if (s.phase == EpisodePhase.Social && ActionsLeft(s) && s.Find(s.playerId).status == ContestantStatus.Active)
            {
                if (!s.alliances.Any(a => a.active && a.members.Contains(s.playerId)))
                {
                    var friend = s.Active.Where(c => !c.isPlayer).OrderByDescending(c => s.Score(c.id, s.playerId)).First();
                    if (s.Score(friend.id, s.playerId) >= 8) return Command(s, EpisodeCommandKind.FormAlliance, friend.id);
                }
                var coldest = s.Active.Where(c => !c.isPlayer).OrderBy(c => s.Score(c.id, s.playerId)).First();
                return Command(s, EpisodeCommandKind.Talk, coldest.id);
            }
            if (s.phase == EpisodePhase.Campaign && VoteRead.Available(s) && s.Find(s.playerId).status == ContestantStatus.Active)
            {
                var voters = NpcVoters(s);
                var unasked = voters.FirstOrDefault(v => !EpisodeEngine.AskedThisWeek(s, v.id));
                if (unasked != null) return Command(s, EpisodeCommandKind.AskVote, unasked.id);
                var unread = voters.FirstOrDefault(v => !EpisodeEngine.ReadThisWeek(s, v.id));
                if (unread != null) return Command(s, EpisodeCommandKind.ReadPerson, unread.id);
                if (ActionsLeft(s))
                {
                    var sheet = VoteRead.Read(s);
                    string wantsOut = sheet.predictedEvicteeId ?? s.nominees.First(id => id != s.playerId);
                    if (s.nominees.Contains(s.playerId))
                    {
                        wantsOut = s.nominees.First(id => id != s.playerId);
                        var unpleaded = voters.FirstOrDefault(v => StrategyRules.CanLobby(s, v.id, LobbyAsk.Vote, s.playerId, out _));
                        if (unpleaded != null)
                            return Command(s, EpisodeCommandKind.Lobby, unpleaded.id, s.playerId, LobbyAsk.Encode(LobbyAsk.Vote, LobbyApproach.Emotional));
                    }
                    foreach (var pact in s.alliances.Where(a => a.active && a.members.Contains(s.playerId) && !s.ledger.calls.Any(k => k.week == s.week && k.allianceId == a.id)))
                    {
                        var ally = voters.FirstOrDefault(v => pact.members.Contains(v.id));
                        if (ally != null && wantsOut != s.playerId) return Command(s, EpisodeCommandKind.CallTheVote, ally.id, wantsOut, pact.id);
                    }
                    var torn = voters.FirstOrDefault(v =>
                    {
                        var read = sheet.voters.FirstOrDefault(r => r.voterId == v.id);
                        return read != null && read.confidence != VoteRead.Firm && read.leaningId != wantsOut
                            && PlayerDeals.CanPropose(s, v.id, DealKind.VoteEvict, wantsOut, out _);
                    });
                    if (torn != null && wantsOut != s.playerId) return Command(s, EpisodeCommandKind.ProposeDeal, torn.id, wantsOut, DealKind.VoteEvict);
                }
            }
            if (s.phase == EpisodePhase.Eviction && !s.evictionResolved && (s.evictionStage == EvictionStage.Voting || s.evictionStage == EvictionStage.Tiebreaker)
                && !s.votes.Any(v => v.voterId == s.playerId) && (EpisodeEngine.Voters(s).Any(v => v.isPlayer) || EpisodeEngine.NeedsPlayerTieBreak(s)))
            {
                string target = VoteRead.Read(s).predictedEvicteeId ?? s.nominees.First(id => id != s.playerId);
                if (target == s.playerId) target = s.nominees.First(id => id != s.playerId);
                return Command(s, EpisodeCommandKind.CastVote, target);
            }
            return null;
        }

        /// <summary>The random player: talks to whoever the coin says, and votes on it.</summary>
        private static EpisodeCommand RandomNext(EpisodeState s, uint seed)
        {
            if (s.pendingDiary != null) return null;
            var beat = AnswerBeat(s);
            if (beat != null) return beat;
            if ((s.phase == EpisodePhase.Social || s.phase == EpisodePhase.Campaign) && ActionsLeft(s) && s.Find(s.playerId).status == ContestantStatus.Active
                && Coin(s, seed, 1) < 0.5)
            {
                var npcs = s.Active.Where(c => !c.isPlayer).ToArray();
                return Command(s, EpisodeCommandKind.Talk, npcs[(int)(Coin(s, seed, 2) * npcs.Length) % npcs.Length].id);
            }
            if (s.phase == EpisodePhase.Eviction && !s.evictionResolved && (s.evictionStage == EvictionStage.Voting || s.evictionStage == EvictionStage.Tiebreaker)
                && !s.votes.Any(v => v.voterId == s.playerId) && (EpisodeEngine.Voters(s).Any(v => v.isPlayer) || EpisodeEngine.NeedsPlayerTieBreak(s)))
            {
                var choices = s.nominees.Where(id => id != s.playerId).ToArray();
                return Command(s, EpisodeCommandKind.CastVote, choices[(int)(Coin(s, seed, 3) * choices.Length) % choices.Length]);
            }
            return null;
        }

        /// <summary>One season to the end under a policy, the default walker filling in what the policy leaves alone; its verdict.</summary>
        private static (GameSense.Report report, EpisodeState final, string error) Play(uint seed, Func<EpisodeState, uint, EpisodeCommand> policy)
        {
            var engine = new EpisodeEngine(Season(seed));
            string error = null;
            int i = 0;
            for (; i < 2500 && engine.Snapshot.phase != EpisodePhase.Finished; i++)
            {
                var s = engine.Snapshot;
                var own = policy(s, seed);
                var ownResult = own == null ? null : engine.Apply(own);
                if (ownResult != null && ownResult.accepted) continue;
                var fallback = EpisodeEngineTests.NextCommand(s);
                var next = engine.Apply(fallback);
                if (!next.accepted)
                {
                    error = "seed " + seed + " week " + s.week + " " + s.phase + "/" + s.evictionStage + ": own " + (own?.kind.ToString() ?? "none")
                        + (ownResult == null ? "" : " (" + ownResult.reason + ")") + "; next " + fallback.kind + " (" + next.reason + ")";
                    break;
                }
            }
            var final = engine.Snapshot;
            if (error == null && final.phase != EpisodePhase.Finished)
                error = "seed " + seed + " ran " + i + " commands and ended in week " + final.week + " " + final.phase + "; last own " + (policy(final, seed)?.kind.ToString() ?? "none");
            return (GameSense.Evaluate(final), final, error);
        }

        private static Sweep Run(string player, IEnumerable<uint> seeds, Func<EpisodeState, uint, EpisodeCommand> policy)
        {
            var sweep = new Sweep { player = player };
            double comps = 0, strategy = 0, social = 0, taken = 0, offered = 0;
            foreach (uint seed in seeds)
            {
                var (report, final, error) = Play(seed, policy);
                sweep.seasons++;
                if (error != null) { sweep.errors++; sweep.firstError = sweep.firstError ?? error; continue; }
                sweep.scores.Add(report.score);
                comps += report.competitions; strategy += report.strategy; social += report.social;
                taken += final.ledger.opportunities.Count(o => o.response == OpportunityResponse.Taken);
                offered += final.ledger.opportunities.Count;
                if (final.winnerId == final.playerId) { sweep.wins++; sweep.winnerScores.Add(report.score); }
                if (final.winnerId == final.playerId || final.runnerUpId == final.playerId) sweep.finalTwos++;
            }
            int n = Math.Max(1, sweep.scores.Count);
            sweep.meanScore = sweep.scores.Count == 0 ? 0 : sweep.scores.Average();
            sweep.meanCompetitions = comps / n; sweep.meanStrategy = strategy / n; sweep.meanSocial = social / n;
            sweep.meanTaken = taken / n; sweep.meanOffered = offered / n;
            return sweep;
        }

        [Test]
        public void AReaderOutscoresARandomPlayerAndTheGameIsNotSolvedByReading()
        {
            var seeds = Enumerable.Range(1, 12).Select(i => (uint)i).ToList();
            var reader = Run("reader", seeds, ReaderNext);
            var random = Run("random", seeds, RandomNext);
            TestContext.WriteLine(reader); TestContext.WriteLine(random);
            Assert.That(reader.errors + random.errors, Is.Zero, "Every season reaches its end: " + (reader.firstError ?? random.firstError));
            Assert.That(reader.meanScore - random.meanScore, Is.GreaterThanOrEqualTo(8), "Game Sense separates a reader from a random player.");
            Assert.That(reader.wins, Is.LessThan(seeds.Count), "The game is not solved by reading.");
        }

#if !UNITY_5_3_OR_NEWER
        /// <summary>The sweep the plan asks for: eighty seasons a player, the numbers every lever weight is tuned against. Run it by name.</summary>
        [Test, Explicit("A report: run it by name.")]
        public void GameSenseReport()
        {
            var seeds = Enumerable.Range(1, 80).Select(i => (uint)i).ToList();
            var reader = Run("reader", seeds, ReaderNext);
            var random = Run("random", seeds, RandomNext);
            TestContext.WriteLine(reader); TestContext.WriteLine(random);
            TestContext.WriteLine("gap " + (reader.meanScore - random.meanScore).ToString("0.0") + " (target 20)");
            TestContext.WriteLine("reader scores: " + string.Join(" ", reader.scores.OrderBy(x => x)));
            TestContext.WriteLine("random scores: " + string.Join(" ", random.scores.OrderBy(x => x)));
        }
#endif
    }
}
