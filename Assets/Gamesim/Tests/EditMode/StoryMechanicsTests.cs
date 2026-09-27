using System;
using System.Collections.Generic;
using System.Linq;
using Gamesim.Simulation;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// The story system's rules one at a time: who resents whom after a nomination, what a grudge
    /// does to the nomination sort, the vote and the jury, who can see an alliance, how a backdoor
    /// lands, how production's ladder climbs, and that none of it moves the season's own stream.
    /// </summary>
    public sealed class StoryMechanicsTests
    {
        private static EpisodeState Season(uint seed = 3, int size = 8) => StorySeasonTests.StorySeason(seed, size);

        private static EpisodeCommand Command(EpisodeState s, EpisodeCommandKind kind) => EpisodeEngineTests.Command(s, kind);

        private static EpisodeState Apply(EpisodeState s, EpisodeCommand c)
        {
            var result = new EpisodeEngine(s).Apply(c);
            Assert.That(result.accepted, Is.True, c.kind + ": " + result.reason);
            return result.state;
        }

        private static List<ContestantState> Npcs(EpisodeState s) => s.contestants.Where(c => !c.isPlayer).ToList();

        // ---------------------------------------------------------------- grudges

        [Test]
        public void AnNpcNominationLeavesBothNomineesResentingTheHeadOfHousehold()
        {
            var s = Season();
            var hoh = Npcs(s)[0];
            s.phase = EpisodePhase.Nomination; s.hohId = hoh.id;
            var after = Apply(s, Command(s, EpisodeCommandKind.Advance));
            Assert.That(after.nominees, Has.Count.EqualTo(2));
            foreach (var nominee in after.nominees.Where(id => id != after.playerId))
                Assert.That(Grudges.Severity(after, nominee, hoh.id), Is.EqualTo(70).Within(0.001),
                    "Each initial nominee holds the web's seventy against the Head of Household.");
        }

        [Test]
        public void AnAlliedNomineeResentsItMore()
        {
            var s = Season();
            var hoh = Npcs(s)[0];
            s.phase = EpisodePhase.Nomination; s.hohId = hoh.id;
            // Everybody else is allied with the Head of Household, so whoever goes up was betrayed.
            foreach (var other in Npcs(s).Skip(1))
                s.alliances.Add(new AllianceState { id = "alliance-test-" + other.id, name = "Test", members = new List<string> { hoh.id, other.id }, active = true });
            var after = Apply(s, Command(s, EpisodeCommandKind.Advance));
            foreach (var nominee in after.nominees.Where(id => id != after.playerId))
                Assert.That(Grudges.Severity(after, nominee, hoh.id), Is.EqualTo(84).Within(0.001));
        }

        [Test]
        public void AGrudgeMovesTheNominationSortAndTheirWordProtects()
        {
            var s = Season();
            var hoh = Npcs(s)[0].id;
            var target = Npcs(s)[1].id;
            Assert.That(StoryConsumers.NominationPreference(s, hoh, target), Is.EqualTo(s.Score(hoh, target)));
            Grudges.Add(s, hoh, target, 60, GrudgeCauses.Story);
            Assert.That(StoryConsumers.NominationPreference(s, hoh, target), Is.EqualTo(s.Score(hoh, target) - 18).Within(0.001));
            s.promises.Add(new PromiseState { id = "promise-t", fromId = hoh, toId = target, kind = PromiseKind.Safety,
                status = PromiseStatus.Active, week = s.week, expiresWeek = s.week + 1 });
            Assert.That(StoryConsumers.NominationPreference(s, hoh, target), Is.EqualTo(s.Score(hoh, target) - 18 + 30).Within(0.001));
        }

        [Test]
        public void GrudgesFadeTwoAWeekAndAreForgivenOnceTheyLikeEachOtherAgain()
        {
            var s = Season();
            string a = Npcs(s)[0].id, b = Npcs(s)[1].id;
            Grudges.Add(s, a, b, 12, GrudgeCauses.Story);
            Grudges.Age(s);
            Assert.That(Grudges.Severity(s, a, b), Is.EqualTo(10));
            s.relationships.First(r => r.fromId == a && r.toId == b).score = 60;
            Grudges.Age(s);
            Assert.That(Grudges.Severity(s, a, b), Is.Zero, "Below ten and liked again: forgiven.");
        }

        [Test]
        public void OnlyHouseguestsHoldGrudges()
        {
            var s = Season();
            Assert.That(Grudges.Add(s, s.playerId, Npcs(s)[0].id, 50, GrudgeCauses.Story), Is.Null);
            Assert.That(s.story.grudges, Is.Empty);
        }

        // ---------------------------------------------------------------- the vote and the jury

        [Test]
        public void AVotersGrudgeAndBondBecomeVoteFactors()
        {
            var s = Season();
            var npcs = Npcs(s);
            s.phase = EpisodePhase.Eviction; s.hohId = npcs[0].id;
            s.nominees = new List<string> { npcs[1].id, npcs[2].id };
            string voter = npcs[3].id;
            var plain = WebEvictionVoting.FromNative(s, voter);
            Assert.That(plain.storyTerms, Is.Empty, "Nothing written, nothing added: the ten factors stay ten.");
            Grudges.Add(s, voter, npcs[1].id, 60, GrudgeCauses.Story);
            Bonds.Form(s, voter, npcs[2].id, BondKinds.RideOrDie, BondStatus.Private, null);
            var options = WebEvictionVoting.FromNative(s, voter);
            Assert.That(options.storyTerms.Single(t => t.nomineeId == npcs[1].id).grudge, Is.EqualTo(-15).Within(0.001));
            Assert.That(options.storyTerms.Single(t => t.nomineeId == npcs[2].id).bond, Is.EqualTo(25));
            var evaluation = WebEvictionVoting.Evaluate(options);
            Assert.That(evaluation.selectedNomineeId, Is.EqualTo(npcs[1].id), "Grudge against one, ride-or-die with the other.");
            Assert.That(evaluation.nomineeEvaluations.SelectMany(n => n.factors).Select(f => f.code), Does.Contain("grudge").And.Contain("bond"));
        }

        [Test]
        public void ABitterJurorIsNotNoise()
        {
            var s = Season();
            string juror = Npcs(s)[0].id, finalist = Npcs(s)[1].id;
            Assert.That(StoryConsumers.JuryStory(s, juror, finalist), Is.Zero);
            Grudges.Add(s, juror, finalist, 70, GrudgeCauses.Nominated);
            Assert.That(StoryConsumers.JuryStory(s, juror, finalist), Is.EqualTo(-10.5).Within(0.001));
            Bonds.Form(s, juror, finalist, BondKinds.Nemesis, BondStatus.Private, null);
            Assert.That(StoryConsumers.JuryStory(s, juror, finalist), Is.EqualTo(-15), "Clamped at fifteen.");
        }

        // ---------------------------------------------------------------- knowledge

        [Test]
        public void AnAllianceCountsOnlyForThoseWhoKnowOfIt()
        {
            var s = Season();
            var npcs = Npcs(s);
            var pact = new AllianceState { id = "alliance-secret", name = "Secret", members = new List<string> { npcs[0].id, npcs[1].id }, active = true };
            s.alliances.Add(pact);
            // No fact yet: the legacy rule, known to everybody.
            Assert.That(Knowledge.AllianceVisibleTo(s, pact, npcs[2].id), Is.True);
            Knowledge.AllianceFormed(s, pact);
            Assert.That(Knowledge.AllianceVisibleTo(s, pact, npcs[2].id), Is.False, "A private alliance is hidden from outsiders.");
            Assert.That(Knowledge.AllianceVisibleTo(s, pact, npcs[1].id), Is.True, "Its members know.");
            double hidden = ThreatAssessment.Assess(s, npcs[2].id, npcs[0].id).Alliance;
            Knowledge.AddKnower(s, Knowledge.Of(s, FactKinds.Alliance, pact.id), npcs[2].id);
            double known = ThreatAssessment.Assess(s, npcs[2].id, npcs[0].id).Alliance;
            Assert.That(known, Is.GreaterThan(hidden), "Once they know, the alliance is part of the threat.");
        }

        // ---------------------------------------------------------------- the backdoor

        [Test]
        public void APlanningHeadOfHouseholdBackdoorsTheCompThreat()
        {
            var s = Season(7, 10);
            var npcs = Npcs(s);
            var hoh = npcs[0];
            hoh.traits = new List<string> { "Strategic", "Analytical" }; // Steady 4: a planner.
            s.phase = EpisodePhase.Nomination; s.hohId = hoh.id;
            // The Head of Household's view of the house, lowest first: the target, then two pawns,
            // then everybody else, with the player liked well enough to stay out of it.
            var target = npcs[1];
            var order = new List<ContestantState> { target, npcs[2], npcs[3] }.Concat(npcs.Skip(4)).ToList();
            for (int i = 0; i < order.Count; i++) s.relationships.First(r => r.fromId == hoh.id && r.toId == order[i].id).score = -50 + i * 10;
            s.relationships.First(r => r.fromId == hoh.id && r.toId == s.playerId).score = 90;
            target.hohWins = 1;

            var after = Apply(s, Command(s, EpisodeCommandKind.Advance));
            Assert.That(after.nominees, Is.EquivalentTo(new[] { npcs[2].id, npcs[3].id }), "The next two go up as pawns.");
            Assert.That(EpisodeEngine.BackdoorPlanned(after), Is.EqualTo(target.id), "The plan names the real target.");

            // A pawn wins the veto and saves themselves; the Head of Household names the target.
            after.phase = EpisodePhase.VetoMeeting;
            after.vetoHolderId = npcs[2].id;
            after.vetoPlayers = new List<string> { hoh.id, npcs[2].id, npcs[3].id, npcs[4].id, npcs[5].id, npcs[6].id };
            Assert.That(EpisodeEngine.NpcVetoSave(after), Is.EqualTo(npcs[2].id));
            var met = Apply(after, Command(after, EpisodeCommandKind.Advance));
            Assert.That(met.nominees, Is.EquivalentTo(new[] { npcs[3].id, target.id }), "Backdoored.");
            Assert.That(Grudges.Severity(met, target.id, hoh.id), Is.GreaterThan(0), "The replacement resents the Head of Household.");
        }

        [Test]
        public void AnHonestHeadOfHouseholdNominatesWhoTheyMean()
        {
            var s = Season(7, 10);
            var npcs = Npcs(s);
            var hoh = npcs[0];
            hoh.traits = new List<string> { "Loyal", "Social" }; // Steady 0, Honest 2: no scheme.
            s.phase = EpisodePhase.Nomination; s.hohId = hoh.id;
            var target = npcs[1];
            var order = new List<ContestantState> { target, npcs[2], npcs[3] }.Concat(npcs.Skip(4)).ToList();
            for (int i = 0; i < order.Count; i++) s.relationships.First(r => r.fromId == hoh.id && r.toId == order[i].id).score = -50 + i * 10;
            s.relationships.First(r => r.fromId == hoh.id && r.toId == s.playerId).score = 90;
            target.hohWins = 1;
            var after = Apply(s, Command(s, EpisodeCommandKind.Advance));
            Assert.That(after.nominees, Is.EquivalentTo(new[] { target.id, npcs[2].id }));
            Assert.That(EpisodeEngine.BackdoorPlanned(after), Is.Null);
        }

        // ---------------------------------------------------------------- production

        [Test]
        public void ProductionsLadderWarnsThenPenalisesThenRemovesInsideAWindow()
        {
            var s = Season(5, 10);
            var npc = Npcs(s)[0].id;
            s.phase = EpisodePhase.Social; s.evictionResolved = true;
            Assert.That(Production.Strike(s, npc, "one"), Is.EqualTo(1));
            Assert.That(Production.Strike(s, npc, "same week"), Is.Zero, "One strike a person a week.");
            Production.For(s, npc, false).lastStrikeWeek = 0;
            Assert.That(Production.Strike(s, npc, "two"), Is.EqualTo(2));
            // The penalty's Have-Not week is the house's own: the next Head of Household competition,
            // the one they sit out, names them whatever it says.
            Assert.That(HaveNots.Apply(s), Is.True, "A story season plays the Have-Nots.");
            Assert.That(s.punishedHaveNots, Does.Contain(npc));
            Assert.That(Production.For(s, npc, false).sitsOutWeek, Is.EqualTo(s.week + 1));
            Production.For(s, npc, false).lastStrikeWeek = 0;
            Assert.That(Production.Strike(s, npc, "three"), Is.EqualTo(3));
            Assert.That(s.story.pendingRemovalId, Is.EqualTo(npc));
        }

        [Test]
        public void TheSixPersonHouseNeverRemovesAnyone()
        {
            var s = Season(5, 6);
            s.phase = EpisodePhase.Social; s.evictionResolved = true;
            Assert.That(Production.RemovalWindow(s), Is.False);
            var npc = Npcs(s)[0].id;
            for (int i = 0; i < 3; i++) { Production.Strike(s, npc, "again"); Production.For(s, npc, false).lastStrikeWeek = 0; }
            Assert.That(s.story.pendingRemovalId, Is.Null);
        }

        [Test]
        public void ARemovedPlayersSeasonStillFinishesWithoutThemOnTheJury()
        {
            var engine = new EpisodeEngine(Season(9, 10));
            // Play to the first post-eviction social window, then have production remove the player.
            for (int i = 0; i < 400; i++)
            {
                var s = engine.Snapshot;
                if (s.phase == EpisodePhase.Social && s.evictionResolved && s.Find(s.playerId).status == ContestantStatus.Active) break;
                Assert.That(engine.Apply(EpisodeEngineTests.NextCommand(s)).accepted, Is.True);
            }
            var state = engine.Snapshot;
            Assert.That(state.Find(state.playerId).status, Is.EqualTo(ContestantStatus.Active), "The fixture's player reaches a post-eviction window.");
            Production.Pending(state, state.playerId);
            Assert.That(state.story.pendingRemovalId, Is.EqualTo(state.playerId));
            var run = StorySeasonTests.Play(state, 9);
            var you = run.final.Find(run.final.playerId);
            Assert.That(you.status, Is.EqualTo(ContestantStatus.Expelled));
            Assert.That(run.final.story.removals.Single().contestantId, Is.EqualTo(you.id));
            Assert.That(run.final.votes.Any(v => v.voterId == you.id), Is.False, "A removed player takes no jury seat.");
            Assert.That(run.final.juryExchanges, Is.Empty, "Nobody asks the finalists anything on their behalf.");
        }

        /// <summary>
        /// The plan's M6 sweep: at every legal removal window of a season - the player's and a
        /// houseguest's removal alike - the season still plays to its finale, validates, and keeps the
        /// removed person off the jury.
        /// </summary>
        [Test]
        public void EveryLegalWindowsRemovalPlaysToTheFinale([Values(7, 8, 12)] int size)
        {
            var engine = new EpisodeEngine(Season((uint)(size * 3 + 1), size));
            int windows = 0;
            for (int i = 0; i < 4000 && engine.Snapshot.phase != EpisodePhase.Finished; i++)
            {
                var s = engine.Snapshot;
                if (s.phase == EpisodePhase.Social && s.evictionResolved && s.socialActions == 0 && Production.RemovalWindow(s))
                {
                    windows++;
                    foreach (var who in new[] { s.playerId, s.Active.Where(c => !c.isPlayer).OrderBy(c => c.id, StringComparer.Ordinal).First().id })
                    {
                        if (s.Find(who).status != ContestantStatus.Active) continue;
                        var fork = s.Clone();
                        Production.Pending(fork, who);
                        var run = StorySeasonTests.Play(fork, size * 10 + windows);
                        var removed = run.final.Find(who);
                        Assert.That(removed.status, Is.EqualTo(ContestantStatus.Expelled), "Size " + size + ", week " + s.week + ": removed.");
                        Assert.That(run.final.votes.Any(v => v.voterId == who), Is.False, "A removed houseguest takes no jury seat.");
                        Assert.That(run.final.story.removals, Has.Count.EqualTo(1), "One removal a season.");
                    }
                }
                Assert.That(engine.Apply(StorySeasonTests.StoryNext(s, size)).accepted, Is.True);
            }
            Assert.That(windows, Is.GreaterThan(0), "Size " + size + " has at least one legal window.");
        }

        // ---------------------------------------------------------------- beside the strategy windows

        /// <summary>A houseguest who dislikes the player and likes everybody else: someone who will confront them.</summary>
        private static string Confronter(EpisodeState s, int index)
        {
            var npc = Npcs(s)[index].id;
            foreach (var edge in s.relationships.Where(e => e.fromId == npc)) edge.score = edge.toId == s.playerId ? -30 : 50;
            return npc;
        }

        [Test]
        public void WhereTheReplyCardsPlayTheyOwnTheHousesApproaches()
        {
            var s = Season(41, 8);
            Assert.That(StrategyRules.Apply(s), Is.True, "A story season plays the strategy windows.");
            s.week = 2; s.phase = EpisodePhase.Social; s.evictionResolved = true;
            for (int i = 0; i < 4; i++)
            {
                var npc = Confronter(s, i);
                Assert.That(NpcSocialActions.Perform(s, npc, NpcActionKind.Confront), Is.True);
            }
            Assert.That(s.replyCards.Count(r => r.kind == ReplyCards.Confrontation), Is.GreaterThan(0), "The reply cards came.");
            Assert.That(s.storylines.Any(x => x.templateId == "confronted"), Is.False, "And no story tells the same confrontation twice.");
        }

        [Test]
        public void WithoutTheWindowsTheStoryTellsTheConfrontation()
        {
            var s = Season(41, 8);
            s.strategyRulesStartWeek = 0;
            s.week = 2; s.phase = EpisodePhase.Social; s.evictionResolved = true;
            for (int i = 0; i < Npcs(s).Count && !s.storylines.Any(x => x.templateId == "confronted"); i++)
                NpcSocialActions.Perform(s, Confronter(s, i), NpcActionKind.Confront);
            Assert.That(s.storylines.Any(x => x.templateId == "confronted"), Is.True, "The story system stands in for the cards.");
            Assert.That(s.replyCards, Is.Empty);
        }

        [Test]
        public void ADecisionWeighsTheWindowsAndTheStoryOnceEach()
        {
            var s = Season(43, 8);
            var npcs = Npcs(s);
            string hoh = npcs[0].id, target = npcs[1].id;
            double score = s.Score(hoh, target);
            Assert.That(EpisodeEngine.NominationWeight(s, hoh, target), Is.EqualTo(score).Within(1e-9), "Nothing written, nothing weighed.");
            s.alliances.Add(new AllianceState { id = "alliance-w", name = "W", members = new List<string> { hoh, target }, active = true });
            Grudges.Add(s, hoh, target, 60, GrudgeCauses.Story);
            Assert.That(EpisodeEngine.NominationWeight(s, hoh, target),
                Is.EqualTo(score + StrategyRules.AllyShield - 18).Within(1e-9), "The windows' ally shield and the story's grudge, each once.");
            // A safety agreement is the windows' to weigh; the story counts only a promise then.
            s.deals.Add(new DealState { id = "deal-w", proposerId = hoh, recipientId = target, type = DealKind.SafetyAgreement,
                status = DealStatus.Active, week = s.week });
            double withDeal = EpisodeEngine.NominationWeight(s, hoh, target);
            Assert.That(withDeal, Is.EqualTo(StrategyRules.NominationReluctance(s, hoh, target) - 18).Within(1e-9),
                "The agreement is not counted by the story as well.");
        }

        [Test]
        public void WithoutHaveNotsAPenaltyIsTheSitOutAlone()
        {
            var s = Season(5, 10);
            s.haveNotRulesStartWeek = 0;
            var npc = Npcs(s)[0].id;
            s.phase = EpisodePhase.Social; s.evictionResolved = true;
            Production.Strike(s, npc, "one"); Production.For(s, npc, false).lastStrikeWeek = 0;
            Assert.That(Production.Strike(s, npc, "two"), Is.EqualTo(2));
            Assert.That(s.punishedHaveNots, Is.Empty);
            Assert.That(Production.For(s, npc, false).sitsOutWeek, Is.EqualTo(s.week + 1));
            Assert.That(EpisodeValidation.TryValidate(s, out var error), Is.True, error);
        }

        /// <summary>
        /// A word with whoever decides belongs to the strategy windows where they play: the new Head
        /// of Household's (after the competition, and the HoH room that is its fuller version) and the
        /// veto holder's (the dilemma). Without the windows, the story's moments stand in.
        /// </summary>
        [Test]
        public void WhereTheWindowsPlayAWordWithWhoeverDecidesIsTheirs()
        {
            var s = Season(47, 8);
            Assert.That(StrategyRules.Apply(s), Is.True, "A story season plays the strategy windows.");
            var npcs = Npcs(s);
            string hoh = npcs[0].id, holder = npcs[1].id;
            s.week = 2; s.phase = EpisodePhase.Nomination; s.hohId = hoh; s.vetoHolderId = holder;
            // On good terms, so the HoH room would have the player up.
            foreach (var edge in s.relationships.Where(e => e.fromId == s.playerId && e.toId == hoh || e.fromId == hoh && e.toId == s.playerId))
                edge.score = 20;
            var moments = new[] { ("after-the-comp", StoryAnchors.HohCrowned), ("hoh-room", StoryAnchors.HohCrowned), ("veto-dilemma", StoryAnchors.VetoWon) };
            foreach (var (arc, anchor) in moments)
                Assert.That(CastAt(s, arc, anchor), Is.Null, arc + ": the windows' lobby or plea is the way in.");
            s.strategyRulesStartWeek = 0;
            foreach (var (arc, anchor) in moments)
                Assert.That(CastAt(s, arc, anchor), Is.Not.Null, arc + ": without the windows, the story stands in.");
        }

        // ---------------------------------------------------------------- the season's stream

        [Test]
        public void AConversationsStoryDrawsNeverMoveTheSeasonsStream()
        {
            for (uint seed = 1; seed <= 20; seed++)
            {
                var on = Season(seed);
                var off = on.Clone();
                off.story = new StoryWorldState();
                on.phase = off.phase = EpisodePhase.Social;
                foreach (var kind in new[] { EpisodeCommandKind.Talk, EpisodeCommandKind.PersonalChat, EpisodeCommandKind.DiscussGame })
                {
                    var target = Npcs(on)[(int)(seed % 5)].id;
                    var a = Command(on, kind); a.targetId = target;
                    var b = Command(off, kind); b.targetId = target;
                    var withStory = Apply(on, a);
                    var without = Apply(off, b);
                    Assert.That(withStory.randomState, Is.EqualTo(without.randomState), "Seed " + seed + " " + kind);
                    on = withStory; off = without;
                }
            }
        }

        // ---------------------------------------------------------------- the arcs a random sweep rarely reaches

        private static ArcBinding CastAt(EpisodeState s, string arcId, string anchor, string talkingTo = null) =>
            EpisodeEngine.Castable(s, new StoryContext(s, anchor, talkingTo), StoryCatalog.Find(arcId));

        [Test]
        public void TheShowmanceCastsTheWarmestRomanceOpenHouseguestYouHaveTimeWith()
        {
            var s = Season(21);
            s.week = 2;
            var partner = Npcs(s).First(c => Lore.RomanceOpen(s, c.id));
            s.relationships.First(r => r.fromId == s.playerId && r.toId == partner.id).score = 40;
            s.relationships.First(r => r.fromId == partner.id && r.toId == s.playerId).score = 40;
            s.story.contacts.Add(new ContactState { npcId = partner.id, rapport = 5, lastWeek = 2, weekCount = 1 });
            var binding = CastAt(s, "late-nights", StoryAnchors.EvictionNight);
            Assert.That(binding, Is.Not.Null);
            Assert.That(binding.Get("PARTNER"), Is.EqualTo(partner.id));
            s.story.romanceStorylines = false;
            Assert.That(CastAt(s, "late-nights", StoryAnchors.EvictionNight), Is.Null, "The season's romance switch is honoured.");
        }

        [Test]
        public void TheFinalTwoPactCastsYourClosestAlly()
        {
            var s = Season(22);
            s.week = 3;
            var ally = Npcs(s)[0];
            s.alliances.Add(new AllianceState { id = "alliance-p", name = "P", members = new List<string> { s.playerId, ally.id }, active = true });
            s.relationships.First(r => r.fromId == s.playerId && r.toId == ally.id).score = 60;
            s.relationships.First(r => r.fromId == ally.id && r.toId == s.playerId).score = 60;
            Assert.That(CastAt(s, "ride-or-die", StoryAnchors.EvictionNight)?.Get("PARTNER"), Is.EqualTo(ally.id));
        }

        [Test]
        public void ARealWinnerKnowsTheHouseIsComingForThem()
        {
            var s = StorySeasonTests.StorySeason(23, 8, CastTemplates.Roster.AllStars);
            var winner = Npcs(s).FirstOrDefault(c => Lore.SheetIn(s, c.id)?.wonSeason == true);
            Assert.That(winner, Is.Not.Null, "An All-Stars house has a real winner in it.");
            // Week one's eviction night: the first moment a season may ask anything (plan §5.2).
            s.phase = EpisodePhase.Social; s.evictionResolved = true;
            Assert.That(CastAt(s, "the-target-on-their-back", StoryAnchors.EvictionNight), Is.Not.Null);
            s.week = 4;
            Assert.That(CastAt(s, "the-target-on-their-back", StoryAnchors.EvictionNight), Is.Null, "Only early in the season.");
        }

        [Test]
        public void RealPeopleNeverPlayRomanceOrConductParts()
        {
            var s = StorySeasonTests.StorySeason(24, 8, CastTemplates.Roster.AllStars);
            foreach (var npc in Npcs(s))
            {
                Assert.That(StoryPeople.IsRealPerson(npc), Is.True, npc.name);
                Assert.That(StoryPeople.Allowed(s, npc.id, StoryPeople.Sensitivity.Romance), Is.False);
                Assert.That(StoryPeople.Allowed(s, npc.id, StoryPeople.Sensitivity.Conduct), Is.False);
                Assert.That(StoryPeople.Allowed(s, npc.id, StoryPeople.Sensitivity.Game), Is.True);
                Assert.That(Lore.RomanceOpen(s, npc.id), Is.False);
            }
        }

        [Test]
        public void TheOddsShownAreTheOddsUsedAndStayInBounds()
        {
            var s = Season(25);
            var subject = Npcs(s)[0];
            var item = new HouseEventState { id = "house-event-x", kind = HouseEventKind.Story, involvedIds = new List<string> { subject.id } };
            var choice = new HouseEventChoice { optionId = "o", checkBase = 50, subjectId = subject.id, approach = Personality.Approach.Warm };
            foreach (var score in new[] { -100.0, -20, 0, 20, 100 })
            {
                s.relationships.First(r => r.fromId == s.playerId && r.toId == subject.id).score = score;
                int chance = StoryOdds.Chance(s, item, choice);
                Assert.That(chance, Is.InRange(StoryOdds.Floor, StoryOdds.Ceiling));
                Assert.That(chance, Is.EqualTo(StoryOdds.Clamp(StoryOdds.Terms(s, item, choice).Sum(t => t.value))));
            }
            Assert.That(StoryOdds.Chance(s, item, new HouseEventChoice { checkBase = -1 }), Is.EqualTo(-1), "An unchecked option is certain.");
        }
    }
}
