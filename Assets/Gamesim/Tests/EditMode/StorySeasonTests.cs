using System;
using System.Collections.Generic;
using System.Linq;
using Gamesim.Simulation;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// Whole seasons played with the story system on: every command goes through the engine's own
    /// validation, so a story that ever writes an invalid state, strands a beat, or wedges the
    /// week fails here. The driver answers story beats, spends social actions to open conversation
    /// beats, and otherwise plays the season the way <see cref="EpisodeEngineTests.NextCommand"/> does.
    /// </summary>
    public sealed class StorySeasonTests
    {
        internal static EpisodeState StorySeason(uint seed, int houseSize = 8,
            CastTemplates.Roster roster = CastTemplates.Roster.Regular)
        {
            var state = SeasonBuilder.Create(new SeasonBuilder.Choice { HouseSize = houseSize, Roster = roster }, seed);
            EpisodeEngine.EnableStory(state);
            // The read rules, as the director switches them on for every season it starts: under them
            // every alliance is a private fact at birth. Never in SeasonBuilder, whose recorded seasons
            // and seeded fixtures would shift.
            EpisodeEngine.EnableRead(state);
            // NPC agency, as the director switches it on for every season it starts: first
            // impressions by temperament, agendas, a Head of Household who weighs threat. Never in
            // SeasonBuilder, for the same reason as the read.
            EpisodeEngine.EnableAgency(state);
            return state;
        }

        internal sealed class Run
        {
            public EpisodeState final;
            public readonly List<string> arcs = new List<string>();
            public int beatsAnswered, socialActions;
        }

        private static readonly EpisodeCommandKind[] Verbs =
        {
            EpisodeCommandKind.Talk, EpisodeCommandKind.SmallTalk, EpisodeCommandKind.PersonalChat,
            EpisodeCommandKind.RelationshipBuilding, EpisodeCommandKind.DiscussGame, EpisodeCommandKind.VentAbout,
            EpisodeCommandKind.ShareSecret,
        };

        /// <summary>A number from the state and a salt, so each season plays differently and replays identically.</summary>
        private static int Mix(EpisodeState s, int salt, int purpose) =>
            (int)(SeededRandom.HashSeed(salt + ":" + s.revision + ":" + purpose) % 1000);

        internal static EpisodeCommand StoryNext(EpisodeState s, int salt)
        {
            var player = s.Find(s.playerId);
            bool active = player.status == ContestantStatus.Active;
            bool socialTime = s.phase == EpisodePhase.Social || s.phase == EpisodePhase.Campaign;
            bool actionsLeft = EpisodeEngine.SocialActionsSpent(s) < EpisodeEngine.SocialActionBudget(s);

            if (active && EpisodeEngine.IntroductionsOpen(s) && !s.openingBeatsSeen.Contains(OpeningBeat.MeetAndGreet))
            {
                var mark = EpisodeEngineTests.Command(s, EpisodeCommandKind.MarkOpeningBeat);
                mark.targetId = OpeningBeat.MeetAndGreet;
                return mark;
            }

            if (active && Mix(s, salt, 1) < 800)
            {
                foreach (var beat in EpisodeEngine.OpenStoryBeats(s))
                {
                    var options = beat.choices.Where(c => !c.locked && (!c.costsAction || (socialTime && actionsLeft))
                                                          && (!c.pickPerson || c.eligibleIds.Any(id => s.Find(id)?.status == ContestantStatus.Active))).ToList();
                    if (options.Count == 0) continue;
                    var choice = options[Mix(s, salt, 2) % options.Count];
                    var answer = EpisodeEngineTests.Command(s, EpisodeCommandKind.ProgressStoryline);
                    answer.targetId = beat.id;
                    answer.secondTargetId = choice.optionId;
                    if (choice.pickPerson)
                    {
                        var eligible = choice.eligibleIds.Where(id => s.Find(id)?.status == ContestantStatus.Active).ToList();
                        answer.text = eligible[Mix(s, salt, 3) % eligible.Count];
                    }
                    return answer;
                }
            }

            if (active && s.phase == EpisodePhase.Social && s.pendingDiary == null && Mix(s, salt, 8) < 60)
            {
                var npcs = s.Active.Where(c => !c.isPlayer).ToList();
                if (npcs.Count >= 2)
                {
                    var first = npcs[Mix(s, salt, 9) % npcs.Count];
                    var second = npcs.Where(n => n.id != first.id).ToList()[Mix(s, salt, 10) % (npcs.Count - 1)];
                    if (EpisodeEngine.ProximityOpen(s, first.id, second.id))
                    {
                        var walk = EpisodeEngineTests.Command(s, EpisodeCommandKind.WitnessProximity);
                        walk.targetId = first.id; walk.secondTargetId = second.id; walk.text = "the kitchen";
                        return walk;
                    }
                }
            }

            if (active && socialTime && actionsLeft && s.pendingDiary == null && Mix(s, salt, 11) < 120)
            {
                // Commitments, so broken words, reckonings and alliance stories have something to stand on.
                var ally = s.Active.Where(c => !c.isPlayer && !s.Allied(s.playerId, c.id) && s.Score(c.id, s.playerId) >= 8)
                    .OrderBy(c => c.id, StringComparer.Ordinal).FirstOrDefault();
                if (ally != null && Mix(s, salt, 12) < 500)
                {
                    var form = EpisodeEngineTests.Command(s, EpisodeCommandKind.FormAlliance);
                    form.targetId = ally.id;
                    return form;
                }
                var promised = s.Active.Where(c => !c.isPlayer && !s.promises.Any(p => p.status == PromiseStatus.Active && p.fromId == s.playerId
                                                                                        && p.toId == c.id && p.kind == PromiseKind.Safety))
                    .OrderBy(c => c.id, StringComparer.Ordinal).FirstOrDefault();
                if (promised != null)
                {
                    var promise = EpisodeEngineTests.Command(s, EpisodeCommandKind.PromiseSafety);
                    promise.targetId = promised.id;
                    return promise;
                }
            }

            if (active && socialTime && actionsLeft && s.pendingDiary == null && Mix(s, salt, 4) < 450)
            {
                var npcs = s.Active.Where(c => !c.isPlayer).ToList();
                if (npcs.Count >= 2)
                {
                    var verb = Verbs[Mix(s, salt, 5) % Verbs.Length];
                    var talk = EpisodeEngineTests.Command(s, verb);
                    var target = npcs[Mix(s, salt, 6) % npcs.Count];
                    talk.targetId = target.id;
                    if (verb == EpisodeCommandKind.VentAbout)
                    {
                        var others = npcs.Where(n => n.id != target.id).ToList();
                        talk.secondTargetId = others[Mix(s, salt, 7) % others.Count].id;
                    }
                    return talk;
                }
            }

            var next = EpisodeEngineTests.NextCommand(s);
            // A player production removed is not a juror, and has no vote to cast.
            if (next.kind == EpisodeCommandKind.CastVote && s.phase == EpisodePhase.Jury && player.status == ContestantStatus.Expelled)
                next.kind = EpisodeCommandKind.Advance;
            return next;
        }

        internal static Run Play(EpisodeState initial, int salt, int maxCommands = 4000)
        {
            var engine = new EpisodeEngine(initial);
            var run = new Run();
            for (int i = 0; i < maxCommands && engine.Snapshot.phase != EpisodePhase.Finished; i++)
            {
                var s = engine.Snapshot;
                var command = StoryNext(s, salt);
                var result = engine.Apply(command);
                Assert.That(result.accepted, Is.True, "salt " + salt + ", week " + s.week + " " + s.phase + " (" + s.evictionStage + "): "
                    + command.kind + " " + command.targetId + " " + command.secondTargetId + ": " + result.reason);
                if (command.kind == EpisodeCommandKind.ProgressStoryline) run.beatsAnswered++;
                if (Array.IndexOf(Verbs, command.kind) >= 0) run.socialActions++;
            }
            run.final = engine.Snapshot;
            run.arcs.AddRange(run.final.storylines.Where(x => x.beatId != null).Select(x => x.templateId));
            Assert.That(run.final.phase, Is.EqualTo(EpisodePhase.Finished), "salt " + salt + " did not finish.");
            Assert.That(EpisodeValidation.TryValidate(run.final, out var error), Is.True, error);
            return run;
        }

#if !UNITY_5_3_OR_NEWER
        // ---------------------------------------------------------------- reach (plan 30 §5, P4)

        /// <summary>
        /// The arcs that step aside where the strategy windows play (the owner's decision and the
        /// windows' own reply cards). They come back as plays that use the windows as their steps (P2);
        /// until then the reach sweep reports them apart.
        /// </summary>
        internal static readonly string[] WindowStepAsides =
            { "after-the-comp", "hoh-room", "veto-dilemma", "confronted", "campaign-pitch", "caught-talking" };

        private static readonly EpisodeCommandKind[] SkilledVerbs =
        {
            EpisodeCommandKind.PersonalChat, EpisodeCommandKind.RelationshipBuilding, EpisodeCommandKind.DiscussGame,
            EpisodeCommandKind.ShareSecret, EpisodeCommandKind.PersonalChat, EpisodeCommandKind.RelationshipBuilding,
            EpisodeCommandKind.SmallTalk, EpisodeCommandKind.Talk, EpisodeCommandKind.VentAbout,
        };

        /// <summary>
        /// A skilled player, for the reach sweep (plan 30 §5): the reader's answers to the story, and a
        /// social game with a shape instead of the harness's scatter - a circle of the three it gets on
        /// with best, alliances only with people who like it back and two at most, a final two with its
        /// closest ally once the house is down to eight, loyalty sworn when it is offered, the circle's
        /// offers taken, the bedroom's talk with somebody open to it, and - a quarter of the times one
        /// is on offer - a rule bent when it pays.
        /// </summary>
        internal static EpisodeCommand SkilledNext(EpisodeState s, int salt)
        {
            var player = s.Find(s.playerId);
            if (player.status != ContestantStatus.Active
                || (EpisodeEngine.IntroductionsOpen(s) && !s.openingBeatsSeen.Contains(OpeningBeat.MeetAndGreet)))
                return StoryNext(s, salt);
            // A player who bends a rule when it pays, now and then: production's own stories need somebody to.
            foreach (var beat in EpisodeEngine.OpenStoryBeats(s))
            {
                var conduct = beat.choices.FirstOrDefault(c => c.conduct && !c.locked && !c.pickPerson && !c.costsAction);
                if (conduct == null || Mix(s, salt, 17) >= 250) continue;
                var rule = EpisodeEngineTests.Command(s, EpisodeCommandKind.ProgressStoryline);
                rule.targetId = beat.id;
                rule.secondTargetId = conduct.optionId;
                return rule;
            }
            var answer = StoryPlayTests.ReaderNext(s, salt);
            if (answer != null) return answer;

            string me = s.playerId;
            bool socialTime = s.phase == EpisodePhase.Social || s.phase == EpisodePhase.Campaign;
            bool actionsLeft = EpisodeEngine.SocialActionsSpent(s) < EpisodeEngine.SocialActionBudget(s);
            var npcs = s.Active.Where(c => !c.isPlayer).OrderBy(c => c.id, StringComparer.Ordinal).ToList();
            double Mutual(string a, string b) => Math.Min(s.Score(a, b), s.Score(b, a));
            var circle = npcs.OrderByDescending(c => Mutual(me, c.id)).ThenBy(c => c.id, StringComparer.Ordinal).Take(3).ToList();

            // Walking in on people: a social player notices who keeps company, half the time the closest pair.
            if (s.phase == EpisodePhase.Social && s.pendingDiary == null && npcs.Count >= 2 && Mix(s, salt, 8) < 60)
            {
                string a, b;
                if (Mix(s, salt, 13) < 500)
                {
                    var pair = (from x in npcs from y in npcs where string.CompareOrdinal(x.id, y.id) < 0
                                orderby Mutual(x.id, y.id) descending, x.id, y.id select new[] { x.id, y.id }).First();
                    a = pair[0]; b = pair[1];
                }
                else
                {
                    a = npcs[Mix(s, salt, 9) % npcs.Count].id;
                    var rest = npcs.Where(n => n.id != a).ToList();
                    b = rest[Mix(s, salt, 10) % rest.Count].id;
                }
                if (EpisodeEngine.ProximityOpen(s, a, b))
                {
                    var walk = EpisodeEngineTests.Command(s, EpisodeCommandKind.WitnessProximity);
                    walk.targetId = a; walk.secondTargetId = b; walk.text = "the kitchen";
                    return walk;
                }
            }

            if (socialTime && s.pendingDiary == null)
            {
                // Not actions: loyalty offered by the circle is sworn, and the circle's offers are taken.
                string oath = s.oathOpportunities.FirstOrDefault(id => circle.Any(c => c.id == id));
                if (oath != null)
                {
                    var swear = EpisodeEngineTests.Command(s, EpisodeCommandKind.SwearLoyalty);
                    swear.targetId = oath;
                    return swear;
                }
                var offer = s.deals.Where(d => d.status == DealStatus.Proposed && d.recipientId == me && circle.Any(c => c.id == d.proposerId))
                    .OrderBy(d => d.id, StringComparer.Ordinal).FirstOrDefault();
                if (offer != null)
                {
                    var accept = EpisodeEngineTests.Command(s, EpisodeCommandKind.RespondToDeal);
                    accept.targetId = offer.id;
                    accept.text = EpisodeEngine.AcceptDeal;
                    return accept;
                }
            }

            if (socialTime && actionsLeft && s.pendingDiary == null && npcs.Count >= 2)
            {
                // A final two with the closest ally once the house is down to eight, once.
                if (s.Active.Count() <= 8 && !s.promises.Any(p => p.fromId == me && p.kind == PromiseKind.FinalTwo))
                {
                    var partner = circle.FirstOrDefault(c => s.Allied(me, c.id));
                    if (partner != null)
                    {
                        var finalTwo = EpisodeEngineTests.Command(s, EpisodeCommandKind.PromiseFinalTwo);
                        finalTwo.targetId = partner.id;
                        return finalTwo;
                    }
                }
                // Alliances only with people who like you back, and two at most.
                if (s.alliances.Count(a => a.active && a.members.Contains(me)) < 2 && Mix(s, salt, 11) < 300)
                {
                    var mate = circle.FirstOrDefault(c => !s.Allied(me, c.id) && Mutual(me, c.id) >= 20);
                    if (mate != null)
                    {
                        var form = EpisodeEngineTests.Command(s, EpisodeCommandKind.FormAlliance);
                        form.targetId = mate.id;
                        return form;
                    }
                }
                // Now and then, safety promised to somebody in the circle who has none from you.
                if (Mix(s, salt, 12) < 60)
                {
                    var promised = circle.FirstOrDefault(c => !s.promises.Any(p => p.status == PromiseStatus.Active && p.fromId == me
                                                                                  && p.toId == c.id && p.kind == PromiseKind.Safety));
                    if (promised != null)
                    {
                        var promise = EpisodeEngineTests.Command(s, EpisodeCommandKind.PromiseSafety);
                        promise.targetId = promised.id;
                        return promise;
                    }
                }
                // Conversation, mostly with the circle.
                if (Mix(s, salt, 4) < 600)
                {
                    var target = circle.Count > 0 && Mix(s, salt, 6) < 700 ? circle[Mix(s, salt, 14) % circle.Count] : npcs[Mix(s, salt, 15) % npcs.Count];
                    var verb = EpisodeEngine.RoomActsOpen(s) && Lore.RomanceOpen(s, target.id) && Mutual(me, target.id) >= 15 && Mix(s, salt, 16) < 400
                        ? EpisodeCommandKind.PillowTalk : SkilledVerbs[Mix(s, salt, 5) % SkilledVerbs.Length];
                    var talk = EpisodeEngineTests.Command(s, verb);
                    talk.targetId = target.id;
                    if (verb == EpisodeCommandKind.VentAbout)
                        talk.secondTargetId = npcs.Where(n => n.id != target.id).OrderBy(n => Mutual(me, n.id)).ThenBy(n => n.id, StringComparer.Ordinal).First().id;
                    return talk;
                }
            }

            var next = EpisodeEngineTests.NextCommand(s);
            if (next.kind == EpisodeCommandKind.CastVote && s.phase == EpisodePhase.Jury && player.status == ContestantStatus.Expelled)
                next.kind = EpisodeCommandKind.Advance;
            return next;
        }

        /// <summary>
        /// Plan 30 P4's reach: every arc must come round for a skilled player. The sweep plays what the
        /// director ships (the read rules on) across the regular eight, twelve and sixteen and the
        /// All-Stars eight, with <see cref="SkilledNext"/>. A report; the window step-asides are listed apart.
        /// </summary>
        [Test, Explicit("A report: run it by name.")]
        public void ReachReport()
        {
            var groups = new List<(int size, CastTemplates.Roster roster, int seasons)>
            {
                (8, CastTemplates.Roster.Regular, 160), (12, CastTemplates.Roster.Regular, 160),
                (16, CastTemplates.Roster.Regular, 80), (8, CastTemplates.Roster.AllStars, 80),
            };
            var counts = StoryCatalog.All.ToDictionary(a => a.id, a => 0, StringComparer.Ordinal);
            int seasons = 0, commands = 0, refused = 0;
            var refusals = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var group in groups)
                for (uint seed = 1; seed <= group.seasons; seed++)
                {
                    var engine = new EpisodeEngine(StorySeason(seed * 17 + (uint)group.size, group.size, group.roster));
                    for (int i = 0; i < 5000 && engine.Snapshot.phase != EpisodePhase.Finished; i++)
                    {
                        var s = engine.Snapshot;
                        var command = SkilledNext(s, (int)seed);
                        commands++;
                        var result = engine.Apply(command);
                        if (result.accepted) continue;
                        refused++;
                        string key = command.kind + ": " + result.reason;
                        refusals[key] = refusals.TryGetValue(key, out var r) ? r + 1 : 1;
                        Assert.That(engine.Apply(EpisodeEngineTests.NextCommand(s)).accepted, Is.True);
                    }
                    seasons++;
                    foreach (var cycle in engine.Snapshot.storylines.Where(x => x.templateId != null && counts.ContainsKey(x.templateId)))
                        counts[cycle.templateId]++;
                }
            TestContext.WriteLine("seasons " + seasons + ", commands " + commands + ", refused " + refused);
            foreach (var pair in refusals.OrderByDescending(p => p.Value).Take(8)) TestContext.WriteLine("    refused " + pair.Value + "x  " + pair.Key);
            TestContext.WriteLine("never: " + string.Join(", ", counts.Where(p => p.Value == 0 && !WindowStepAsides.Contains(p.Key)).Select(p => p.Key).OrderBy(k => k, StringComparer.Ordinal)));
            TestContext.WriteLine("window step-asides: " + string.Join(", ", WindowStepAsides.Select(id => id + " " + counts[id])));
            TestContext.WriteLine("rare (1-3): " + string.Join(", ", counts.Where(p => p.Value > 0 && p.Value <= 3).OrderBy(p => p.Value).ThenBy(p => p.Key, StringComparer.Ordinal)
                .Select(p => p.Key + " " + p.Value)));
            TestContext.WriteLine("most: " + string.Join(", ", counts.OrderByDescending(p => p.Value).ThenBy(p => p.Key, StringComparer.Ordinal).Take(16)
                .Select(p => p.Key + " " + p.Value)));
            TestContext.WriteLine("arcs started a season: " + (counts.Where(p => StoryCatalog.Find(p.Key)?.play == null).Sum(p => p.Value) / (double)seasons).ToString("0.0")
                + ", plays: " + (counts.Where(p => StoryCatalog.Find(p.Key)?.play != null).Sum(p => p.Value) / (double)seasons).ToString("0.0"));
        }

        /// <summary>
        /// Plan 31's P3a check: every season seeds two or three threads, and each reaches its climax or a
        /// stated end. Played by the skilled player over the regular eight, twelve and sixteen. A report.
        /// </summary>
        [Test, Explicit("A report: run it by name.")]
        public void ThreadsReport()
        {
            var seededPerSeason = new Dictionary<int, int>();
            var endings = new SortedDictionary<string, int>(StringComparer.Ordinal);
            var chapters = new SortedDictionary<string, int[]>(StringComparer.Ordinal);
            int seasons = 0, threads = 0, stillRunning = 0, chapterCount = 0, survived = 0, survivedWithTwo = 0;
            foreach (var size in new[] { 8, 12, 16 })
                for (uint seed = 1; seed <= 40; seed++)
                {
                    var engine = new EpisodeEngine(StorySeason(seed * 23 + (uint)size, size));
                    bool pastWeekOne = false;
                    for (int i = 0; i < 5000 && engine.Snapshot.phase != EpisodePhase.Finished; i++)
                    {
                        var s = engine.Snapshot;
                        pastWeekOne |= s.week >= 2 && s.Find(s.playerId).status == ContestantStatus.Active;
                        var command = SkilledNext(s, (int)seed);
                        if (!engine.Apply(command).accepted) Assert.That(engine.Apply(EpisodeEngineTests.NextCommand(s)).accepted, Is.True);
                    }
                    var final = engine.Snapshot;
                    Assert.That(EpisodeValidation.TryValidate(final, out var error), Is.True, error);
                    seasons++;
                    var views = EpisodeEngine.Threads(final);
                    if (pastWeekOne) { survived++; if (views.Count >= 2) survivedWithTwo++; }
                    seededPerSeason[views.Count] = seededPerSeason.TryGetValue(views.Count, out var n) ? n + 1 : 1;
                    foreach (var view in views)
                    {
                        threads++;
                        if (view.ending == null) stillRunning++;
                        string key = view.kind + " -> " + (view.ending ?? "still running");
                        endings[key] = endings.TryGetValue(key, out var e) ? e + 1 : 1;
                        foreach (var chapter in view.chapters)
                        {
                            chapterCount++;
                            if (!chapters.TryGetValue(view.kind + " " + chapter.arcId, out var row)) chapters[view.kind + " " + chapter.arcId] = row = new int[4];
                            row[Array.IndexOf(new[] { ThreadChapterResults.Open, ThreadChapterResults.Landed, ThreadChapterResults.Missed, ThreadChapterResults.Never }, chapter.result)]++;
                        }
                    }
                }
            TestContext.WriteLine("seasons " + seasons + ", threads " + threads + " (" + (threads / (double)seasons).ToString("0.0") + " a season), still running at the end " + stillRunning
                + ", chapters " + (chapterCount / (double)Math.Max(1, threads)).ToString("0.0") + " a thread");
            TestContext.WriteLine("threads a season: " + string.Join(", ", seededPerSeason.OrderBy(p => p.Key).Select(p => p.Key + ": " + p.Value))
                + "; seasons the player saw week two " + survived + ", of them with two or three threads " + survivedWithTwo);
            foreach (var pair in endings) TestContext.WriteLine("    " + pair.Key.PadRight(28) + pair.Value);
            foreach (var pair in chapters) TestContext.WriteLine("    " + pair.Key.PadRight(34) + "open " + pair.Value[0] + ", landed " + pair.Value[1] + ", missed " + pair.Value[2] + ", never " + pair.Value[3]);
        }

        // For the Unity-free run: Unity's batch runner executes an [Explicit] test.
        /// <summary>A report, not a check: which arcs a sweep of seasons reaches, how often, and how they end.</summary>
        [Test, Explicit("Diagnostic: prints arc coverage for tuning.")]
        public void ArcCoverageReport()
        {
            var counts = new Dictionary<string, int>();
            var endings = new Dictionary<string, int>();
            int beats = 0, seasons = 0, expelled = 0, removals = 0;
            foreach (var size in new[] { 6, 8, 12, 16 })
                for (uint seed = 1; seed <= 20; seed++)
                {
                    var run = Play(StorySeason(seed * 13 + (uint)size, size), (int)seed);
                    seasons++;
                    beats += run.beatsAnswered;
                    foreach (var cycle in run.final.storylines.Where(x => x.beatId != null))
                    {
                        counts[cycle.templateId] = counts.TryGetValue(cycle.templateId, out var n) ? n + 1 : 1;
                        string key = cycle.templateId + " -> " + cycle.endingId;
                        endings[key] = endings.TryGetValue(key, out var m) ? m + 1 : 1;
                    }
                    if (run.final.Find(run.final.playerId).status == ContestantStatus.Expelled) expelled++;
                    removals += run.final.story.removals.Count;
                }
            TestContext.WriteLine("seasons " + seasons + ", beats answered " + beats + ", removals " + removals + " (player " + expelled + ")");
            foreach (var arc in StoryCatalog.All.OrderBy(a => a.id))
                TestContext.WriteLine((counts.TryGetValue(arc.id, out var n) ? n : 0).ToString().PadLeft(5) + "  " + arc.id);
            foreach (var pair in endings.OrderBy(p => p.Key)) TestContext.WriteLine(pair.Value.ToString().PadLeft(5) + "  " + pair.Key);
        }
#endif

        [Test]
        public void TheStorySystemIsOffUntilSomethingSwitchesItOn()
        {
            var state = SeasonBuilder.Create(new SeasonBuilder.Choice { HouseSize = 8 }, 7u);
            Assert.That(state.story, Is.Not.Null);
            Assert.That(state.story.rulesStartWeek, Is.Zero);
            Assert.That(EpisodeEngine.StoryOn(state), Is.False);
            EpisodeEngine.EnableStory(state);
            Assert.That(EpisodeEngine.StoryOn(state), Is.True);
            Assert.That(state.story.rulesVersion, Is.EqualTo(StoryRules.Current));
            Assert.That(state.story.lore.Count, Is.EqualTo(state.contestants.Count(c => !c.isPlayer)));
            Assert.That(EpisodeValidation.TryValidate(state, out var error), Is.True, error);
        }

        [Test]
        public void AnEmptyStoryStateReproducesEveryConsumerExactly()
        {
            var state = StorySeason(11u);
            var npcs = state.contestants.Where(c => !c.isPlayer).ToList();
            foreach (var a in npcs)
                foreach (var b in state.contestants.Where(x => x.id != a.id))
                {
                    Assert.That(StoryConsumers.NominationPreference(state, a.id, b.id), Is.EqualTo(state.Score(a.id, b.id)));
                    Assert.That(StoryConsumers.SavePreference(state, a.id, b.id), Is.EqualTo(state.Score(a.id, b.id)));
                    Assert.That(StoryConsumers.WillNotSave(state, a.id, b.id), Is.False);
                    Assert.That(StoryConsumers.VoteGrudge(state, a.id, b.id), Is.Zero);
                    Assert.That(StoryConsumers.VoteBond(state, a.id, b.id), Is.Zero);
                    Assert.That(StoryConsumers.JuryStory(state, a.id, b.id), Is.Zero);
                }
        }

        [Test]
        public void EightHouseSeasonsFinishWithEveryCommandValidated()
        {
            var arcs = new HashSet<string>();
            int beats = 0;
            for (uint seed = 1; seed <= 24; seed++)
            {
                var run = Play(StorySeason(seed), (int)seed);
                beats += run.beatsAnswered;
                foreach (var arc in run.arcs) arcs.Add(arc);
            }
            Assert.That(beats, Is.GreaterThan(24), "Seasons with the story system on should put beats in front of the player.");
            Assert.That(arcs.Count, Is.GreaterThanOrEqualTo(15), "Arcs seen: " + string.Join(", ", arcs.OrderBy(x => x)));
        }

        [Test]
        public void EveryHouseSizeAndRosterFinishes([Values(6, 12, 16)] int size, [Values(CastTemplates.Roster.Regular, CastTemplates.Roster.AllStars)] CastTemplates.Roster roster)
        {
            for (uint seed = 1; seed <= 6; seed++)
                Play(StorySeason(seed * 31, size, roster), (int)(seed * 7 + size));
        }

        [Test]
        public void TheDefaultSceneSeasonFinishesWithTheStoryOn()
        {
            for (uint seed = 0; seed < 12; seed++)
            {
                var state = ContentCatalog.Create(seed);
                EpisodeEngine.EnableStory(state);
                Play(state, (int)seed);
            }
        }

        [Test]
        public void AnsweringAStoryBeatNeverDrawsFromTheSeasonsStream()
        {
            int checkedBeats = 0;
            for (uint seed = 1; seed <= 12 && checkedBeats < 12; seed++)
            {
                var engine = new EpisodeEngine(StorySeason(seed));
                for (int i = 0; i < 1500 && engine.Snapshot.phase != EpisodePhase.Finished; i++)
                {
                    var s = engine.Snapshot;
                    var command = StoryNext(s, (int)seed);
                    var result = engine.Apply(command);
                    Assert.That(result.accepted, Is.True, result.reason);
                    if (command.kind != EpisodeCommandKind.ProgressStoryline) continue;
                    Assert.That(result.state.randomState, Is.EqualTo(s.randomState),
                        "Answering " + command.secondTargetId + " moved the season's main stream.");
                    checkedBeats++;
                }
            }
            Assert.That(checkedBeats, Is.GreaterThan(0));
        }

        [Test]
        public void ReplayingAStorySeasonReproducesItExactly()
        {
            var a = new EpisodeEngine(StorySeason(5u));
            var b = new EpisodeEngine(StorySeason(5u));
            for (int i = 0; i < 3000 && a.Snapshot.phase != EpisodePhase.Finished; i++)
            {
                var command = StoryNext(a.Snapshot, 5);
                Assert.That(a.Apply(command).accepted, Is.True);
                Assert.That(b.Apply(command).accepted, Is.True);
                if (i % 25 == 0) b = new EpisodeEngine(b.Snapshot);
            }
            var x = a.Snapshot; var y = b.Snapshot;
            Assert.That(y.winnerId, Is.EqualTo(x.winnerId));
            Assert.That(y.randomState, Is.EqualTo(x.randomState));
            Assert.That(y.events.Select(e => e.text), Is.EqualTo(x.events.Select(e => e.text)));
            Assert.That(y.story.grudges.Select(g => g.holderId + g.targetId + g.severity), Is.EqualTo(x.story.grudges.Select(g => g.holderId + g.targetId + g.severity)));
        }
    }
}
