using System;
using System.Collections.Generic;
using System.Linq;
using Gamesim.Simulation;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// Genuine seasons on dialogue rules 4 (DIALOGUE-PLAN D4) for the tests that play the veto question: the shipped season
    /// enrolled by the literal 4 (rules 4 is defined and not offered, so nothing else starts one), walked by the plain public
    /// walk with the player playing every competition at zero, to each veto competition still ahead where the player is
    /// nominated, the house's asks answered with their own neutral exits. Past the enrolment nothing is assigned by hand
    /// except where a test says so. One walk, cached for the whole run.
    /// </summary>
    internal static class DialogueRules4Season
    {
        internal const string Template = DialogueVetoQuestion.TemplateId;

        internal static EpisodeState Start(uint seed, int houseSize = 8)
        {
            var fresh = SeasonBuilder.Create(new SeasonBuilder.Choice { HouseSize = houseSize }, seed);
            ShippedRules.ApplyFresh(fresh);
            Assert.That(fresh.dialogueRulesVersion, Is.Zero, "A shipped season starts on dialogue rules 0.");
            fresh.dialogueRulesVersion = DialogueRules.Rules4;
            return fresh;
        }

        internal sealed class Stop
        {
            internal uint Seed;
            internal EpisodeState State;
            internal string[] People;
            /// <summary>Who wins the veto from this state when the player plays it at zero; the question draws nothing, so it is the same after one.</summary>
            internal string Winner;
        }

        private static List<Stop> stops;

        /// <summary>Every veto competition the walk met in seeds 1 to 60 (weeks 1 to 5) with the player nominated and somebody to ask.</summary>
        internal static IReadOnlyList<Stop> Stops()
        {
            if (stops != null) return stops;
            var found = new List<Stop>();
            for (uint seed = 1; seed <= 60; seed++) found.AddRange(StopsOf(seed));
            TestContext.Out.WriteLine("Rules-4 veto competitions with the player nominated: " + found.Count + ".");
            Assert.That(found, Is.Not.Empty, "No rules-4 season of 60 met a veto competition with the player nominated and somebody to ask.");
            return stops = found;
        }

        /// <summary>One seed's walk to week 5, and each veto competition on it with the player nominated and somebody to ask.</summary>
        internal static List<Stop> StopsOf(uint seed)
        {
            var found = new List<Stop>();
            var engine = new EpisodeEngine(Start(seed));
            int lastWeek = 0;
            for (int step = 0; step < 600; step++)
            {
                var s = engine.Snapshot;
                if (s.week > 5 || s.phase == EpisodePhase.Finished || s.Find(s.playerId).status != ContestantStatus.Active) break;
                if (s.phase == EpisodePhase.Veto && !s.competitionResolved && s.nominees.Contains(s.playerId) && s.week != lastWeek)
                {
                    lastWeek = s.week;
                    DialogueRules2Season.CloseAsks(engine);
                    s = engine.Snapshot;
                    var people = s.vetoPlayers.Where(id => id != s.playerId && DialogueRules.CanStart(s, id, out _)).ToArray();
                    if (people.Length > 0)
                    {
                        var probe = new EpisodeEngine(s.Clone());
                        var played = Accepted(probe, Compete(s));
                        found.Add(new Stop { Seed = seed, State = s.Clone(), People = people, Winner = played.vetoHolderId });
                    }
                }
                var next = EpisodeEngineTests.NextCommand(engine.Snapshot);
                if (next.kind == EpisodeCommandKind.Compete) next.performance = 0;
                DialogueRules2Season.Accepted(engine, next);
            }
            return found;
        }

        internal static EpisodeState Accepted(EpisodeEngine engine, EpisodeCommand command) => DialogueRules2Season.Accepted(engine, command);

        internal static EpisodeCommand Compete(EpisodeState s)
        {
            var c = DialogueRules2Season.Command(s, EpisodeCommandKind.Compete);
            c.performance = 0;
            return c;
        }

        /// <summary>The entry the HUD builds for a veto player: the public plan, never a hand-made payload.</summary>
        internal static EpisodeCommand Ask(EpisodeState s, string npcId)
        {
            Assert.That(DialogueRules.CanAskVetoQuestion(s, npcId), Is.True, "The game page offers the question.");
            Assert.That(DialogueRules.TryBuildEntryCommand(s, npcId, out var plan), Is.True, "The HUD's own entry plan.");
            Assert.That(plan.text, Is.EqualTo(Template));
            plan.id = "dialogue-r4-ask-" + s.revision + "-" + npcId;
            return plan;
        }

        internal static EpisodeCommand Choice(EpisodeState s, string option) => DialogueRules2Season.Choice(s, option);

        internal static DialogueEpisodeState Question(EpisodeState s, string npcId) =>
            s.dialogue.episodes.Single(e => e.occasionKind == DialogueVetoQuestion.Kind && e.npcId == npcId && e.week == s.week);

        internal static string Suffix(DialogueResponseState response) => response.responseKey.Substring(Template.Length + 1);

        /// <summary>From the veto competition to the house's decision: the competition at zero, the meeting's opening, the decision.</summary>
        internal static EpisodeState[] ToTheDecision(EpisodeEngine engine)
        {
            var states = new List<EpisodeState> { Accepted(engine, Compete(engine.Snapshot)) };
            for (int step = 0; step < 8 && !engine.Snapshot.vetoResolved; step++)
            {
                var next = EpisodeEngineTests.NextCommand(engine.Snapshot);
                Assert.That(next.kind == EpisodeCommandKind.Advance || next.kind == EpisodeCommandKind.SkipDiary, Is.True,
                    "The house decides the veto: " + next.kind);
                states.Add(Accepted(engine, next));
            }
            Assert.That(engine.Snapshot.vetoResolved, Is.True, "The veto was decided.");
            return states.ToArray();
        }

        internal sealed class Promise
        {
            internal Stop Stop;
            internal string Maker;
            internal EpisodeState Asked, Promised, Thanked;
        }

        private static List<Promise> promises;

        /// <summary>
        /// Every yes the walk's stops can give, made a promise, and its twin thanked instead: each stop's question to the
        /// veto's eventual winner, and to one other veto player. Only yeses whose promise was given.
        /// </summary>
        internal static IReadOnlyList<Promise> Promises()
        {
            if (promises != null) return promises;
            var found = Stops().SelectMany(stop => PromisesAt(stop, false)).ToList();
            TestContext.Out.WriteLine("Rules-4 promises: " + found.Count + " (" + found.Count(p => p.Maker == p.Stop.Winner) + " by the winner).");
            return promises = found;
        }

        /// <summary>One stop's yeses made promises, each with its thanked twin: the eventual winner's, and unless only that is asked, one other's.</summary>
        private static IEnumerable<Promise> PromisesAt(Stop stop, bool winnerOnly)
        {
            var asked = stop.People.Where(id => id == stop.Winner).Concat(winnerOnly ? Enumerable.Empty<string>() : stop.People.Where(id => id != stop.Winner).Take(1));
            foreach (string npc in asked.ToList())
            {
                var engine = new EpisodeEngine(stop.State.Clone());
                var opened = Accepted(engine, Ask(stop.State, npc));
                if (Question(opened, npc).statement.answer != DialogueVetoQuestion.Yes) continue;
                var promised = Accepted(new EpisodeEngine(opened.Clone()), Choice(opened, DialogueVetoQuestion.AskPromise));
                if (Suffix(Question(promised, npc).responses[1]) != DialogueVetoQuestion.PromiseMade) continue;
                var thanked = Accepted(new EpisodeEngine(opened.Clone()), Choice(opened, DialogueVetoQuestion.Thank));
                yield return new Promise { Stop = stop, Maker = npc, Asked = opened, Promised = promised, Thanked = thanked };
            }
        }

        /// <summary>
        /// The first promise whose maker wins the veto and whose meeting the house decides with the given outcome: among the
        /// cached walk's first, then on further seeds (61 to 240), each walked only as far as the search needs.
        /// </summary>
        internal static (Promise promise, EpisodeState[] with, EpisodeState[] without) Settled(string status)
        {
            var winners = Promises().Where(p => p.Maker == p.Stop.Winner)
                .Concat(Enumerable.Range(61, 180).SelectMany(seed => StopsOf((uint)seed)).SelectMany(stop => PromisesAt(stop, true)));
            int tried = 0;
            foreach (var promise in winners)
            {
                tried++;
                var with = ToTheDecision(new EpisodeEngine(promise.Promised.Clone()));
                var deal = with[with.Length - 1].deals.Single(d => d.type == DealKind.VetoIf);
                if (deal.status != status) continue;
                var without = ToTheDecision(new EpisodeEngine(promise.Thanked.Clone()));
                TestContext.Out.WriteLine("Rules-4 " + status + " promise, the " + tried + "th by a winner tried: seed " + promise.Stop.Seed
                    + ", week " + promise.Promised.week + ", maker " + promise.Maker
                    + (promise.Promised.nominees.Contains(promise.Maker) ? " (a fellow nominee)." : "."));
                return (promise, with, without);
            }
            Assert.Fail("No promise of " + tried + " by the veto's winner in seeds 1 to 240 settled " + status + ".");
            return default;
        }

        /// <summary>Every relationship row between two houseguests, neither of them the player, as "from>to" to its score.</summary>
        internal static Dictionary<string, double> NpcScores(EpisodeState s) =>
            s.relationships.Where(r => r.fromId != s.playerId && r.toId != s.playerId)
                .ToDictionary(r => r.fromId + ">" + r.toId, r => r.score, StringComparer.Ordinal);

        internal static double Score(EpisodeState s, string from, string to) =>
            s.relationships.FirstOrDefault(r => r.fromId == from && r.toId == to)?.score ?? 0;
    }
}
