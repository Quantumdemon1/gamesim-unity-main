using System;
using System.Linq;
using Gamesim.Simulation;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// Plays (plan 30 §2): an offer taken on or turned down, a goal checked against the season
    /// rather than the arc's own bookkeeping, an outcome at the goal or the deadline, and a
    /// receipt that says what changed.
    /// </summary>
    public sealed class StoryPlayTests
    {
        private static EpisodeCommand Command(EpisodeState s, EpisodeCommandKind kind) => EpisodeEngineTests.Command(s, kind);

        private static EpisodeState Apply(EpisodeState s, EpisodeCommand c)
        {
            var result = new EpisodeEngine(s).Apply(c);
            Assert.That(result.accepted, Is.True, c.kind + ": " + result.reason);
            return result.state;
        }

        /// <summary>A real history played with the story off to the first moment this holds, the story switched on there.</summary>
        private static EpisodeState At(Func<EpisodeState, bool> where, uint seed = 31)
        {
            var engine = new EpisodeEngine(SeasonBuilder.Create(new SeasonBuilder.Choice { HouseSize = 8 }, seed));
            for (int i = 0; i < 800 && !where(engine.Snapshot); i++)
                Assert.That(engine.Apply(EpisodeEngineTests.NextCommand(engine.Snapshot)).accepted, Is.True);
            var s = engine.Snapshot;
            Assert.That(where(s), Is.True, "The fixture reaches its moment.");
            Assert.That(s.Find(s.playerId).status, Is.EqualTo(ContestantStatus.Active), "The fixture's player is in the house.");
            EpisodeEngine.EnableStory(s, s.week);
            return s;
        }

        private static bool AfterEviction(EpisodeState s) =>
            s.week >= 2 && s.phase == EpisodePhase.Social && s.evictionResolved && s.pendingDiary == null;

        private static bool AtNominations(EpisodeState s) =>
            s.week >= 2 && s.phase == EpisodePhase.Nomination && s.nominees.Count == 0 && s.hohId != s.playerId;

        private static StorylineState Cycle(EpisodeState s, string arcId) => s.storylines.Last(x => x.templateId == arcId);

        private static HouseEventState Open(EpisodeState s, StorylineState cycle) =>
            s.houseEvents.Single(e => e.cycleId == cycle.id && !e.resolved);

        private static EpisodeState Answer(EpisodeState s, string arcId, string optionId)
        {
            var command = Command(s, EpisodeCommandKind.ProgressStoryline);
            command.targetId = Open(s, Cycle(s, arcId)).id;
            command.secondTargetId = optionId;
            return Apply(s, command);
        }

        private static void Score(EpisodeState s, string from, string to, double value) =>
            s.relationships.First(r => r.fromId == from && r.toId == to).score = value;

        /// <summary>One houseguest warm to the player and nobody else: whoever their-word casts.</summary>
        private static ContestantState OneFriend(EpisodeState s)
        {
            var npcs = s.Active.Where(c => !c.isPlayer).OrderBy(c => c.id, StringComparer.Ordinal).ToList();
            foreach (var npc in npcs) Score(s, npc.id, s.playerId, 0);
            s.promises.RemoveAll(p => p.toId == s.playerId);
            s.deals.RemoveAll(d => d.type == DealKind.SafetyAgreement);
            Score(s, npcs[0].id, s.playerId, 30);
            return npcs[0];
        }

        [Test]
        public void APlayIsAnOfferUntilItIsTakenOn()
        {
            var s = At(AfterEviction);
            var friend = OneFriend(s);
            Assert.That(EpisodeEngine.StartStory(s, "their-word", StoryAnchors.EvictionNight), Is.True, "Their Word casts.");
            var offer = Open(s, Cycle(s, "their-word"));
            Assert.That(offer.choices.Select(c => c.optionId), Is.EquivalentTo(new[] { PlayOptions.TakeItOn, PlayOptions.NotNow }));
            var view = EpisodeEngine.Plays(s).Single();
            Assert.That(view.takenOn, Is.False, "Offered, not yet taken on.");
            Assert.That(view.openEventId, Is.EqualTo(offer.id));
            Assert.That(view.currency, Is.EqualTo(PlayCurrencies.Trust));
            Assert.That(view.deadline, Is.EqualTo(StoryAnchors.EvictionNight));
            Assert.That(view.goal, Does.Contain(friend.name), "The goal names who it is about.");

            var after = Answer(s, "their-word", PlayOptions.TakeItOn);
            var cycle = Cycle(after, "their-word");
            Assert.That(EpisodeEngine.TakenOn(cycle), Is.True);
            Assert.That(Open(after, cycle).contentId, Does.EndWith(":ask"), "Taking it on puts its first step in front of you at once.");
            Assert.That(EpisodeEngine.Plays(after).Single().takenOn, Is.True);
        }

        [Test]
        public void TurningAPlayDownLetsItComeRoundOnceMore()
        {
            var s = At(AfterEviction);
            OneFriend(s);
            Assert.That(EpisodeEngine.StartStory(s, "their-word", StoryAnchors.EvictionNight), Is.True);
            var first = Answer(s, "their-word", PlayOptions.NotNow);
            var refused = Cycle(first, "their-word");
            Assert.That(refused.endingId, Is.EqualTo(PlayEndings.Declined));
            Assert.That(first.story.cooldowns.Single(c => c.key == "tpl:their-word").untilWeek, Is.EqualTo(first.week + 1),
                "The first refusal may come round again next week.");
            Assert.That(EpisodeEngine.Plays(first), Is.Empty, "A play turned down is off the plays page.");

            // Next week it is offered again, and a second refusal rests it for the play's own cooldown.
            // The new week, before its story has asked anything: the play may be offered again.
            var later = Apply(first, Command(first, EpisodeCommandKind.Advance));
            Assert.That(later.week, Is.EqualTo(first.week + 1), "A week later.");
            OneFriend(later);
            Assert.That(EpisodeEngine.StartStory(later, "their-word", StoryAnchors.EvictionNight), Is.True, "It comes round once more.");
            var second = Answer(later, "their-word", PlayOptions.NotNow);
            Assert.That(second.story.cooldowns.Single(c => c.key == "tpl:their-word").untilWeek,
                Is.EqualTo(second.week + StoryCatalog.Find("their-word").cooldownWeeks), "A second refusal rests it as long as any play.");
        }

        [Test]
        public void TheSecretAlliance_TradingWhatYouKnowWinsItAndSaysWho()
        {
            var s = At(AtNominations);
            var npcs = s.Active.Where(c => !c.isPlayer).OrderBy(c => c.id, StringComparer.Ordinal).ToList();
            // A secret pair the player cannot see, and another alliance the player knows about to trade.
            var secret = NpcAlliances.FormFromStory(s, new[] { npcs[0].id, npcs[1].id }.ToList());
            Knowledge.AllianceFormed(s, secret);
            Assert.That(Knowledge.AllianceVisibleTo(s, secret, s.playerId), Is.False, "The fixture's alliance is secret.");
            var known = NpcAlliances.FormFromStory(s, new[] { npcs[2].id, npcs[3].id }.ToList());
            Knowledge.AllianceFormed(s, known);
            Knowledge.MakeKnown(s, Knowledge.Of(s, FactKinds.Alliance, known.id), FactVisibility.Public);
            // Only the secret one is hidden: every other alliance in the fixture is out in the open or gone.
            foreach (var other in s.alliances.Where(a => a != secret && a != known)) other.active = false;

            Assert.That(EpisodeEngine.StartStory(s, "the-secret-alliance", StoryAnchors.HohCrowned), Is.True, "The Secret Alliance casts.");
            s = Answer(s, "the-secret-alliance", PlayOptions.TakeItOn);
            var dig = Open(s, Cycle(s, "the-secret-alliance"));
            Assert.That(dig.choices.Single(c => c.optionId == "trade-what-you-know").locked, Is.False, "Knowing one alliance buys another.");
            s = Answer(s, "the-secret-alliance", "trade-what-you-know");

            var cycle = Cycle(s, "the-secret-alliance");
            Assert.That(cycle.endingId, Is.EqualTo(PlayEndings.Won), "Found out: won on the spot.");
            Assert.That(Knowledge.AllianceVisibleTo(s, s.alliances.Single(a => a.id == secret.id), s.playerId), Is.True);
            Assert.That(s.events.Any(e => e.kind == StoryLog.Play && e.text.StartsWith("The Secret Alliance: You found out who")), Is.True,
                "The play says how it went.");
            var receipt = s.events.Where(e => e.kind == StoryLog.Receipt).Select(e => e.text).ToList();
            Assert.That(receipt, Has.Some.EqualTo("You learned: " + npcs[0].name + " and " + npcs[1].name + " are working together."),
                "The receipt says who, from the effect that ran.");
            Assert.That(s.events.Where(e => e.kind == StoryLog.Receipt).All(e => e.audienceIds.SequenceEqual(new[] { s.playerId })), Is.True,
                "Receipts are the player's alone.");
        }

        [Test]
        public void StayOffTheBlock_IsDecidedByTheFinalBlock([Values(true, false)] bool winsThemOver)
        {
            var s = At(AtNominations);
            string hoh = s.hohId;
            var others = s.Active.Where(c => !c.isPlayer && c.id != hoh).OrderBy(c => c.id, StringComparer.Ordinal).ToList();
            // In the Head of Household's bottom two: a grudge puts the player there.
            Grudges.Add(s, hoh, s.playerId, 80, GrudgeCauses.Story);
            Assert.That(EpisodeEngine.StartStory(s, "stay-off-the-block", StoryAnchors.HohCrowned), Is.True, "Stay Off the Block casts.");
            s = Answer(s, "stay-off-the-block", PlayOptions.TakeItOn);
            s = Answer(s, "stay-off-the-block", "lay-low");

            // However the player got there - here, the house simply changing its mind - the final block decides it.
            Grudges.Ease(s, hoh, s.playerId, 100);
            if (winsThemOver)
            {
                Score(s, hoh, s.playerId, 90);
                foreach (var other in others.Take(2)) Score(s, hoh, other.id, -90);
            }
            else
            {
                // Nothing shields the player: no shared alliance, deal or promise with the Head of Household.
                string player = s.playerId;
                foreach (var alliance in s.alliances.Where(a => a.members.Contains(hoh) && a.members.Contains(player))) alliance.active = false;
                s.deals.RemoveAll(d => (d.proposerId == hoh && d.recipientId == player) || (d.proposerId == player && d.recipientId == hoh));
                s.promises.RemoveAll(p => (p.fromId == hoh && p.toId == player) || (p.fromId == player && p.toId == hoh));
                Score(s, hoh, player, -90);
                foreach (var other in others) Score(s, hoh, other.id, 60);
            }
            for (int i = 0; i < 40 && s.phase != EpisodePhase.Campaign; i++)
            {
                var next = EpisodeEngineTests.NextCommand(s);
                // On the losing side the player throws the veto competition: nobody saves them.
                if (!winsThemOver && next.kind == EpisodeCommandKind.Compete) next.performance = 0;
                s = Apply(s, next);
            }
            Assert.That(s.phase, Is.EqualTo(EpisodePhase.Campaign), "The veto meeting is over.");
            bool onTheBlock = s.nominees.Contains(s.playerId);
            Assert.That(Cycle(s, "stay-off-the-block").endingId, Is.EqualTo(onTheBlock ? PlayEndings.Lost : PlayEndings.Won),
                "The final block decides it.");
            Assert.That(onTheBlock, Is.EqualTo(!winsThemOver), "This fixture's week goes the way the house leans.");
        }

        [Test]
        public void TheirWord_APromiseMadeAnyWayWinsIt()
        {
            var s = At(AfterEviction);
            var friend = OneFriend(s);
            Assert.That(EpisodeEngine.StartStory(s, "their-word", StoryAnchors.EvictionNight), Is.True);
            s = Answer(s, "their-word", PlayOptions.TakeItOn);
            // The goal is read from the season: a promise made any way counts.
            s.promises.Add(new PromiseState
            {
                id = "promise-fixture", fromId = friend.id, toId = s.playerId, kind = PromiseKind.Safety,
                status = PromiseStatus.Active, week = s.week, expiresWeek = s.week + 1,
            });
            s = Answer(s, "their-word", "leave-it");
            Assert.That(Cycle(s, "their-word").endingId, Is.EqualTo(PlayEndings.Won));
            var view = EpisodeEngine.Plays(s).Single();
            Assert.That(view.ending, Is.EqualTo(PlayEndings.Won));
            Assert.That(view.outcome, Is.EqualTo(friend.name + " gave you " + StoryPeople.Pronouns(friend).their + " word."));
        }

        [Test]
        public void SettleIt_CountsTheGrudgeDownAndSettlesBelowTwenty()
        {
            var s = At(AfterEviction);
            var rival = s.Active.Where(c => !c.isPlayer).OrderBy(c => c.id, StringComparer.Ordinal).First();
            foreach (var npc in s.Active.Where(c => !c.isPlayer)) Grudges.Ease(s, npc.id, s.playerId, 100);
            Grudges.Add(s, rival.id, s.playerId, 60, GrudgeCauses.Story);
            Assert.That(EpisodeEngine.StartStory(s, "settle-it", StoryAnchors.EvictionNight), Is.True, "Settle It casts.");
            s = Answer(s, "settle-it", PlayOptions.TakeItOn);
            var cycle = Cycle(s, "settle-it");
            Assert.That(EpisodeEngine.ProgressOf(s, cycle).have, Is.EqualTo(0));
            Grudges.Ease(s, rival.id, s.playerId, Grudges.Severity(s, rival.id, s.playerId) - 30);
            Assert.That(EpisodeEngine.ProgressOf(s, cycle).have, Is.EqualTo(1), "Below where it started to count: part of the way.");
            Grudges.Ease(s, rival.id, s.playerId, Grudges.Severity(s, rival.id, s.playerId) - 10);
            Assert.That(EpisodeEngine.ProgressOf(s, cycle).Met, Is.True);
            s = Answer(s, "settle-it", "let-it-ride");
            Assert.That(Cycle(s, "settle-it").endingId, Is.EqualTo(PlayEndings.Won));
        }

        [Test]
        public void BuildTheNumbers_ThePlayersOwnAlliancesCount()
        {
            var s = At(AfterEviction);
            var npcs = s.Active.Where(c => !c.isPlayer).OrderBy(c => c.id, StringComparer.Ordinal).ToList();
            foreach (var alliance in s.alliances.Where(a => a.members.Contains(s.playerId))) alliance.active = false;
            foreach (var npc in npcs) Score(s, npc.id, s.playerId, 0);
            Score(s, npcs[0].id, s.playerId, 40); Score(s, npcs[1].id, s.playerId, 35);
            Score(s, npcs[0].id, npcs[1].id, 20); Score(s, npcs[1].id, npcs[0].id, 20);
            Assert.That(EpisodeEngine.StartStory(s, "build-the-numbers", StoryAnchors.EvictionNight), Is.True, "Build the Numbers casts.");
            s = Answer(s, "build-the-numbers", PlayOptions.TakeItOn);
            var cycle = Cycle(s, "build-the-numbers");
            Assert.That(EpisodeEngine.ProgressOf(s, cycle).need, Is.EqualTo(2));
            // An alliance the player made their own way, before the pitch: it counts all the same.
            s.alliances.Add(new AllianceState
            {
                id = "alliance-fixture", name = "The Fixture Pact", active = true,
                members = new[] { s.playerId, npcs[0].id, npcs[1].id }.ToList(),
            });
            s = Answer(s, "build-the-numbers", "win-them-over");
            Assert.That(Cycle(s, "build-the-numbers").endingId, Is.EqualTo(PlayEndings.Won));
        }

        [Test]
        public void KnowThem_WhatYouLearnAnyWayCounts()
        {
            var s = At(AfterEviction);
            // Only one stranger to get to know: every other houseguest is someone the player already knows something about.
            var npcs = s.Active.Where(c => !c.isPlayer && !StoryPeople.IsRealPerson(s, c.id) && Lore.FactsOf(s, c.id).Count() >= 2)
                .OrderBy(c => c.id, StringComparer.Ordinal).ToList();
            var subject = npcs.First();
            foreach (var other in npcs.Skip(1)) Lore.Learn(s, Lore.FactsOf(s, other.id).First().id);
            Score(s, s.playerId, subject.id, 10);
            Assert.That(EpisodeEngine.StartStory(s, "know-them", StoryAnchors.EvictionNight), Is.True, "Know Them casts.");
            s = Answer(s, "know-them", PlayOptions.TakeItOn);
            var cycle = Cycle(s, "know-them");
            var facts = Lore.FactsOf(s, subject.id).ToList();
            Lore.Learn(s, facts[0].id);
            Assert.That(EpisodeEngine.ProgressOf(s, cycle).have, Is.EqualTo(1), "Half way: one thing learned.");
            Lore.Learn(s, facts[1].id);
            s = Answer(s, "know-them", "leave-it");
            Assert.That(Cycle(s, "know-them").endingId, Is.EqualTo(PlayEndings.Won), "Two things learned, however: won.");
        }

        [Test]
        public void TheFavour_AHookGainedAnyWayWinsIt()
        {
            var s = At(x => x.week >= 2 && x.phase == EpisodePhase.Campaign && x.nominees.Count > 0 && !x.nominees.Contains(x.playerId));
            var nominee = s.nominees.Select(s.Find).OrderBy(c => c.id, StringComparer.Ordinal).First();
            foreach (var other in s.nominees.Where(id => id != nominee.id)) Score(s, other, s.playerId, -20);
            Score(s, nominee.id, s.playerId, 10);
            Assert.That(EpisodeEngine.StartStory(s, "the-favour", StoryAnchors.BlockSet), Is.True, "The Favour casts.");
            s = Answer(s, "the-favour", PlayOptions.TakeItOn);
            Hooks.Create(s, s.playerId, nominee.id, null);
            s = Answer(s, "the-favour", "stay-out");
            Assert.That(Cycle(s, "the-favour").endingId, Is.EqualTo(PlayEndings.Won));
            Assert.That(EpisodeEngine.Plays(s).Single().outcome, Does.StartWith(nominee.name + " owes you one"));
        }

        [Test]
        public void StirThePot_ACrackIsPartOfTheWayAndAGrudgeWinsIt()
        {
            var s = At(AfterEviction);
            // The house's strongest competitor, and somebody who trusts the player to turn against them.
            var strongest = s.Active.Where(c => !c.isPlayer).OrderByDescending(c => c.hohWins + c.vetoWins)
                .ThenByDescending(c => c.stats.competition).ThenBy(c => c.id, StringComparer.Ordinal).First();
            var trusting = s.Active.Where(c => !c.isPlayer && c.id != strongest.id).OrderBy(c => c.id, StringComparer.Ordinal).First();
            Score(s, trusting.id, s.playerId, 20);
            Grudges.Ease(s, trusting.id, strongest.id, 100);
            Assert.That(EpisodeEngine.StartStory(s, "stir-the-pot", StoryAnchors.EvictionNight), Is.True, "Stir the Pot casts.");
            s = Answer(s, "stir-the-pot", PlayOptions.TakeItOn);
            var cycle = Cycle(s, "stir-the-pot");
            string pawn = cycle.cast.Single(r => r.role == "PAWN").contestantId, target = cycle.cast.Single(r => r.role == "TARGET").contestantId;
            Grudges.Add(s, pawn, target, 25, GrudgeCauses.Story);
            Assert.That(EpisodeEngine.ProgressOf(s, cycle).have, Is.EqualTo(1), "A crack is part of the way.");
            // A grudge that exists stacks at half: 25 and half of 40 is one the pawn will act on.
            Grudges.Add(s, pawn, target, 40, GrudgeCauses.Story);
            s = Answer(s, "stir-the-pot", "leave-them");
            Assert.That(Cycle(s, "stir-the-pot").endingId, Is.EqualTo(PlayEndings.Won), "A grudge the pawn will act on: won.");
        }

        [Test]
        public void AGrudgeAPlayCausesBetweenOthersIsNamedInItsReceipt()
        {
            var s = At(AfterEviction);
            var npcs = s.Active.Where(c => !c.isPlayer).OrderBy(c => c.id, StringComparer.Ordinal).ToList();
            var lines = PlayReceipts.For(s, new[]
            {
                new StoryEffectState { kind = StoryEffects.Grudge, fromId = npcs[0].id, toId = npcs[1].id, amount = 45 },
                new StoryEffectState { kind = StoryEffects.Grudge, fromId = npcs[1].id, toId = s.playerId, amount = 30 },
                new StoryEffectState { kind = StoryEffects.Hook, fromId = s.playerId, toId = npcs[2].id },
            }).ToList();
            Assert.That(lines, Is.EqualTo(new[]
            {
                npcs[0].name + " is holding it against " + npcs[1].name + ".",
                npcs[1].name + " is holding it against you.",
                npcs[2].name + " owes you a favour.",
            }));
        }

        [Test]
        public void PlaysBelongToThePlaysRules()
        {
            var s = At(AfterEviction);
            OneFriend(s);
            s.story.rulesVersion = StoryRules.Production;
            Assert.That(EpisodeEngine.StartStory(s, "their-word", StoryAnchors.EvictionNight), Is.False,
                "A season on the rules before plays never sees one.");
        }

#if !UNITY_5_3_OR_NEWER
        // ---------------------------------------------------------------- P0: the measurement (plan 30 §6, §9)

        /// <summary>
        /// How much story a season holds and whether reading people wins plays: the same seasons
        /// played by the harness's random player and by a reader, who takes every play on and picks
        /// the option with the best odds the game shows. A report, not a check; the owner's "not all
        /// wins are equal" holds only if the reader wins clearly more. Compiled out of Unity, whose
        /// batch runner executes explicit tests.
        /// </summary>
        [Test, Explicit("A report: run it by name.")]
        public void PlaysReport()
        {
            foreach (bool reader in new[] { false, true })
            {
                int seasons = 0, decisions = 0, offered = 0, taken = 0, won = 0, part = 0, lost = 0;
                var fired = new System.Collections.Generic.HashSet<string>();
                var byPlay = new System.Collections.Generic.SortedDictionary<string, int[]>(StringComparer.Ordinal);
                foreach (int size in new[] { 8, 12 })
                    for (uint seed = 1; seed <= 20; seed++)
                    {
                        var engine = new EpisodeEngine(StorySeasonTests.StorySeason(seed * 13 + (uint)size, size));
                        for (int i = 0; i < 4000 && engine.Snapshot.phase != EpisodePhase.Finished; i++)
                        {
                            var s = engine.Snapshot;
                            var command = reader ? ReaderNext(s, (int)seed) ?? StorySeasonTests.StoryNext(s, (int)seed) : StorySeasonTests.StoryNext(s, (int)seed);
                            if (!engine.Apply(command).accepted) Assert.That(engine.Apply(EpisodeEngineTests.NextCommand(s)).accepted, Is.True);
                        }
                        var final = engine.Snapshot;
                        seasons++;
                        foreach (var cycle in final.storylines.Where(x => x.templateId != null))
                        {
                            fired.Add(cycle.templateId);
                            decisions += cycle.path.Count(p => p.result != StoryResults.Lapsed && p.result != StoryResults.Npc);
                            if (StoryCatalog.Find(cycle.templateId)?.play == null) continue;
                            offered++;
                            if (!byPlay.TryGetValue(cycle.templateId, out var row)) byPlay[cycle.templateId] = row = new int[4];
                            row[0]++;
                            if (!EpisodeEngine.TakenOn(cycle)) continue;
                            taken++; row[1]++;
                            if (cycle.endingId == PlayEndings.Won) { won++; row[2]++; }
                            else if (cycle.endingId == PlayEndings.Part) part++;
                            else if (cycle.endingId == PlayEndings.Lost) { lost++; row[3]++; }
                        }
                    }
                int decided = won + part + lost;
                TestContext.WriteLine((reader ? "READER" : "RANDOM") + ": seasons " + seasons
                    + ", story decisions/season " + (decisions / (double)seasons).ToString("0.0")
                    + ", plays offered/season " + (offered / (double)seasons).ToString("0.0")
                    + ", taken " + taken + ", won " + won + ", part " + part + ", lost " + lost
                    + ", win rate " + (decided == 0 ? 0 : 100.0 * won / decided).ToString("0") + "%"
                    + ", arcs fired " + fired.Count + " of " + StoryCatalog.All.Count);
                foreach (var pair in byPlay)
                    TestContext.WriteLine("    " + pair.Key.PadRight(22) + " offered " + pair.Value[0] + ", taken " + pair.Value[1]
                                          + ", won " + pair.Value[2] + ", lost " + pair.Value[3]);
            }
        }

        /// <summary>
        /// The reader: answers every open beat, takes every play on, and picks the option with the
        /// best odds it is shown (a certain option counts as a sure thing), never a lapse while
        /// something else is open. Null when there is nothing to answer.
        /// </summary>
        private static EpisodeCommand ReaderNext(EpisodeState s, int salt)
        {
            if (s.Find(s.playerId).status != ContestantStatus.Active) return null;
            bool socialTime = s.phase == EpisodePhase.Social || s.phase == EpisodePhase.Campaign;
            bool actionsLeft = EpisodeEngine.SocialActionsSpent(s) < EpisodeEngine.SocialActionBudget(s);
            foreach (var beat in EpisodeEngine.OpenStoryBeats(s))
            {
                var options = beat.choices.Where(c => !c.locked && !c.conduct && !c.pickPerson
                                                      && (!c.costsAction || (socialTime && actionsLeft))).ToList();
                if (options.Count == 0) continue;
                var choice = options.FirstOrDefault(c => c.optionId == PlayOptions.TakeItOn)
                             ?? options.Where(c => c.optionId != beat.lapseOptionId)
                                 .OrderByDescending(c => c.checkBase < 0 ? 100 : StoryOdds.Chance(s, beat, c))
                                 .ThenBy(c => c.optionId, StringComparer.Ordinal).FirstOrDefault()
                             ?? options.First();
                var answer = EpisodeEngineTests.Command(s, EpisodeCommandKind.ProgressStoryline);
                answer.targetId = beat.id;
                answer.secondTargetId = choice.optionId;
                return answer;
            }
            return null;
        }
#endif
    }
}
