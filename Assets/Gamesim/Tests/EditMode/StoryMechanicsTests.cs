using System;
using System.Collections.Generic;
using System.Linq;
using Gamesim.Simulation;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// The story system's rules one at a time: who resents whom after a nomination or a veto, what a
    /// grudge does to the nomination sort, the vote and the jury, who can see an alliance, how a
    /// backdoor lands, how production's ladder climbs, and that none of it moves the season's own stream.
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

        // ---------------------------------------------------------------- the veto's grudges (story rules 10, Blame)
        //
        // Each rule is played twice from the same veto meeting: under threads, held there by name so it stays the
        // rules-9 twin when Current moves, and under Blame, opted in by assignment after the story is switched on,
        // as tests opt a season in until its enrolment (RoleRulesTests' AtRoles).

        /// <summary>
        /// A self-save forced nobody (STORY-RULES-10-PLAN §3.1): under Blame the replacement writes nothing against a
        /// veto holder who pulled themselves off the block; under threads, the forty it always was. The Head of
        /// Household who named them is resented at seventy (84 between allies) under both. The rule draws nothing,
        /// mints nothing and says nothing: the twins' streams, sequences and logs are the same.
        /// </summary>
        [Test]
        public void ASelfSaveForcesNobody()
        {
            var meeting = SelfSaveMeeting(out string hoh, out string holder);
            Assert.That(meeting.story.grudges, Is.Empty, "Nobody holds anything yet.");
            var threads = Resolved(meeting, StoryRules.Threads);
            var blame = Resolved(meeting, StoryRules.Blame);
            string replacement = Replacement(meeting, threads, blame);

            Assert.That(Grudges.Severity(threads, replacement, holder), Is.EqualTo(40).Within(0.001), "Under threads, forty against the holder.");
            Assert.That(Row(threads, replacement, holder).cause, Is.EqualTo(GrudgeCauses.ReplacementVeto));
            Assert.That(Row(blame, replacement, holder), Is.Null, "Under Blame a self-save writes nothing against the holder.");
            double againstHoh = meeting.Allied(replacement, hoh) ? 84 : 70;
            foreach (var after in new[] { threads, blame })
            {
                Assert.That(Grudges.Severity(after, replacement, hoh), Is.EqualTo(againstHoh).Within(0.001), "The Head of Household's seventy is unchanged.");
                Assert.That(Row(after, replacement, hoh).cause, Is.EqualTo(GrudgeCauses.Replacement));
            }
            Assert.That(blame.randomState, Is.EqualTo(threads.randomState), "The rule draws nothing,");
            Assert.That(blame.nextSequence, Is.EqualTo(threads.nextSequence), "mints nothing,");
            Assert.That(blame.events.Count, Is.EqualTo(threads.events.Count), "and says nothing.");
        }

        /// <summary>
        /// A veto used on somebody else was the holder's choice, and still stings (STORY-RULES-10-PLAN §3.1): under Blame
        /// at <see cref="EpisodeEngine.ReplacementVetoAtBlame"/>, half the Head of Household's seventy. That is under the
        /// forty line, so the replacement is no longer one of the holders the house's gang-ups and the pariah count read;
        /// it is thirty or more, so a bitter juror still reads it. Under threads, the forty it always was.
        /// </summary>
        [Test]
        public void AVetoForSomebodyElseStingsAtHalfTheSeventy()
        {
            var meeting = SaveForAnotherMeeting(out string hoh, out string holder);
            Assert.That(meeting.story.grudges, Is.Empty, "Nobody holds anything yet.");
            var threads = Resolved(meeting, StoryRules.Threads);
            var blame = Resolved(meeting, StoryRules.Blame);
            string replacement = Replacement(meeting, threads, blame);
            Assert.That(EpisodeEngine.ReplacementVetoAtBlame, Is.EqualTo(70 / 2), "Half the Head of Household's seventy.");

            Assert.That(Grudges.Severity(threads, replacement, holder), Is.EqualTo(40).Within(0.001), "Under threads, forty against the holder.");
            Assert.That(Grudges.HoldersAgainst(threads, holder, 40), Does.Contain(replacement), "On the forty line under threads.");
            Assert.That(Grudges.Severity(blame, replacement, holder), Is.EqualTo(EpisodeEngine.ReplacementVetoAtBlame).Within(0.001),
                "Under Blame a veto for somebody else stings at thirty-five.");
            Assert.That(Row(blame, replacement, holder).cause, Is.EqualTo(GrudgeCauses.ReplacementVeto), "The same cause, a smaller number.");
            Assert.That(Grudges.HoldersAgainst(blame, holder, 40), Does.Not.Contain(replacement), "Under the forty line the gang-ups read.");
            Assert.That(blame.story.bonds.Any(b => (b.aId == replacement && b.bId == holder) || (b.aId == holder && b.bId == replacement)), Is.False,
                "No bond between them, so the jury term is the grudge's alone.");
            Assert.That(StoryConsumers.JuryStory(blame, replacement, holder), Is.EqualTo(-0.15 * EpisodeEngine.ReplacementVetoAtBlame).Within(0.001),
                "At thirty or more a bitter juror still reads it.");
            foreach (var after in new[] { threads, blame })
                Assert.That(Grudges.Severity(after, replacement, hoh), Is.EqualTo(meeting.Allied(replacement, hoh) ? 84 : 70).Within(0.001),
                    "The Head of Household's seventy is unchanged.");
        }

        /// <summary>
        /// One rule for whoever holds the veto (STORY-RULES-10-PLAN decision 3), the player included, whose own command
        /// names who is saved: pulling themselves off the block, the player is not resented for it under Blame; using it on
        /// somebody else, they are, at thirty-five. Under threads, forty either way.
        /// </summary>
        [TestCase(true)] [TestCase(false)]
        public void ThePlayersVetoFollowsTheSameRule(bool selfSave)
        {
            var meeting = Season();
            var npcs = Npcs(meeting);
            string hoh = npcs[0].id, you = meeting.playerId, saved = selfSave ? you : npcs[1].id;
            SetTheVetoMeeting(meeting, hoh, new[] { saved, npcs[2].id }, you, npcs[3].id);
            Assert.That(meeting.story.grudges, Is.Empty, "Nobody holds anything yet.");
            var threads = Resolved(meeting, StoryRules.Threads, saved);
            var blame = Resolved(meeting, StoryRules.Blame, saved);
            string replacement = Replacement(meeting, threads, blame);

            Assert.That(Grudges.Severity(threads, replacement, you), Is.EqualTo(40).Within(0.001), "Under threads, forty against the player.");
            if (selfSave) Assert.That(Row(blame, replacement, you), Is.Null, "Under Blame the player's self-save forces nobody either.");
            else Assert.That(Grudges.Severity(blame, replacement, you), Is.EqualTo(EpisodeEngine.ReplacementVetoAtBlame).Within(0.001),
                "Under Blame the player's veto for somebody else stings at thirty-five.");
        }

        /// <summary>
        /// The pariah's usual route (STORY-RULES-10-PLAN §1), built by hand: a past Head of Household's two nominees still
        /// hold seventy against them when, nominated in their turn, they win the veto and save themselves. Under threads
        /// the replacement's fresh forty is the third holder at the forty line, a pariah's count; under Blame there is no
        /// third, so two.
        /// </summary>
        [Test]
        public void APastHeadOfHouseholdsSelfSaveMakesNoThirdHolder()
        {
            var meeting = Season();
            var npcs = Npcs(meeting);
            string pastHoh = npcs[0].id, first = npcs[1].id, second = npcs[2].id, hoh = npcs[3].id;
            Grudges.Add(meeting, first, pastHoh, 70, GrudgeCauses.Nominated);
            Grudges.Add(meeting, second, pastHoh, 70, GrudgeCauses.Nominated);
            SetTheVetoMeeting(meeting, hoh, new[] { pastHoh, npcs[4].id }, pastHoh, npcs[5].id);
            Assert.That(Grudges.HoldersAgainst(meeting, pastHoh, 40), Is.EquivalentTo(new[] { first, second }), "Their two nominees.");
            Assert.That(EpisodeEngine.NpcVetoSave(meeting), Is.EqualTo(pastHoh), "On the block, they save themselves.");
            var threads = Resolved(meeting, StoryRules.Threads);
            var blame = Resolved(meeting, StoryRules.Blame);
            string replacement = Replacement(meeting, threads, blame);
            Assert.That(new[] { first, second }, Does.Not.Contain(replacement), "A third houseguest is named.");

            var underThreads = Grudges.HoldersAgainst(threads, pastHoh, 40);
            Assert.That(underThreads, Has.Count.EqualTo(3), "Under threads, a third holder: " + string.Join(", ", underThreads));
            Assert.That(underThreads, Does.Contain(replacement));
            Assert.That(Grudges.HoldersAgainst(blame, pastHoh, 40), Is.EquivalentTo(new[] { first, second }), "Under Blame, still two.");
        }

        /// <summary>
        /// Every story gate is a lower bound, so a season at Roles plays Blame's rules as well (the ladder's own rule: Roles
        /// waits for every milestone below it). The rule reads the story's version, not <see cref="EpisodeEngine.RolesOn"/>.
        /// </summary>
        [Test]
        public void RolesPlaysTheBlameRules()
        {
            var selfSave = SelfSaveMeeting(out _, out string holder);
            var self = Resolved(selfSave, StoryRules.Roles);
            Assert.That(Row(self, Replacement(selfSave, self), holder), Is.Null, "A self-save at Roles forces nobody.");

            var forAnother = SaveForAnotherMeeting(out _, out string saver);
            var other = Resolved(forAnother, StoryRules.Roles);
            Assert.That(Grudges.Severity(other, Replacement(forAnother, other), saver), Is.EqualTo(EpisodeEngine.ReplacementVetoAtBlame).Within(0.001),
                "A veto for somebody else at Roles stings at thirty-five.");
        }

        /// <summary>
        /// Whole seasons at Blame, at eight and at twelve: every command the engine takes is validated before it is installed
        /// (<see cref="StorySeasonTests.Play"/>), each season finishes and validates, and none moves off its story rules.
        /// </summary>
        [Test]
        public void SeasonsAtTheBlameRulesFinishWithEveryCommandValidated([Values(8, 12)] int size)
        {
            for (uint seed = 1; seed <= 3; seed++)
            {
                var s = Season(seed * 41 + (uint)size, size);
                s.story.rulesVersion = StoryRules.Blame;
                var run = StorySeasonTests.Play(s, (int)seed * 13 + size);
                Assert.That(run.final.story.rulesVersion, Is.EqualTo(StoryRules.Blame), "Nothing moves a season's story rules.");
            }
        }

        /// <summary>
        /// Week one's veto meeting, set by hand: a houseguest Head of Household, <paramref name="nominees"/> on the block and
        /// the veto with <paramref name="holder"/>. The Head of Household dislikes <paramref name="named"/>, likes the rest
        /// of the house and the player most, so a used veto's replacement is a houseguest.
        /// </summary>
        private static void SetTheVetoMeeting(EpisodeState s, string hoh, string[] nominees, string holder, string named)
        {
            s.phase = EpisodePhase.VetoMeeting;
            s.hohId = hoh;
            s.nominees = nominees.ToList();
            s.vetoHolderId = holder;
            s.vetoPlayers = new[] { hoh }.Concat(nominees).Concat(new[] { holder }).Concat(s.Active.Select(c => c.id))
                .Distinct().Take(EpisodeEngine.VetoPlayerCount(s.Active.Count())).ToList();
            foreach (var edge in s.relationships.Where(r => r.fromId == hoh))
                edge.score = edge.toId == named ? -60 : edge.toId == s.playerId ? 90 : 60;
        }

        /// <summary>A houseguest holds the veto from the block, and saves themselves.</summary>
        private static EpisodeState SelfSaveMeeting(out string hoh, out string holder)
        {
            var s = Season();
            var npcs = Npcs(s);
            hoh = npcs[0].id; holder = npcs[1].id;
            SetTheVetoMeeting(s, hoh, new[] { holder, npcs[2].id }, holder, npcs[3].id);
            Assert.That(EpisodeEngine.NpcVetoSave(s), Is.EqualTo(holder), "On the block, the holder saves themselves.");
            return s;
        }

        /// <summary>A houseguest holds the veto off the block, fond of one nominee and cold to the other, and saves the one.</summary>
        private static EpisodeState SaveForAnotherMeeting(out string hoh, out string holder)
        {
            var s = Season();
            var npcs = Npcs(s);
            hoh = npcs[0].id; holder = npcs[3].id;
            string friend = npcs[1].id, other = npcs[2].id;
            SetTheVetoMeeting(s, hoh, new[] { friend, other }, holder, npcs[4].id);
            s.relationships.First(r => r.fromId == holder && r.toId == friend).score = 90;
            s.relationships.First(r => r.fromId == holder && r.toId == other).score = -60;
            Assert.That(EpisodeEngine.NpcVetoSave(s), Is.EqualTo(friend), "Off the block, the holder saves a friend.");
            return s;
        }

        /// <summary>
        /// The meeting decided on a copy at <paramref name="rules"/>: a houseguest holder's by Advance, the player's by their
        /// own command saving <paramref name="playerSaves"/>.
        /// </summary>
        private static EpisodeState Resolved(EpisodeState meeting, int rules, string playerSaves = null)
        {
            var s = meeting.Clone();
            s.story.rulesVersion = rules;
            var command = Command(s, playerSaves == null ? EpisodeCommandKind.Advance : EpisodeCommandKind.ResolveVeto);
            if (playerSaves != null) { command.useVeto = true; command.targetId = playerSaves; }
            var after = Apply(s, command);
            Assert.That(after.vetoResolved, Is.True, "The veto meeting is decided.");
            Assert.That(after.story.rulesVersion, Is.EqualTo(rules));
            return after;
        }

        /// <summary>Who went up in the saved nominee's place: the same houseguest in every twin, never the player.</summary>
        private static string Replacement(EpisodeState meeting, params EpisodeState[] decided)
        {
            var named = decided.Select(d => d.nominees.Single(id => !meeting.nominees.Contains(id))).Distinct().ToList();
            Assert.That(named, Has.Count.EqualTo(1), "The twins name the same replacement: " + string.Join(", ", named));
            Assert.That(named[0], Is.Not.EqualTo(meeting.playerId), "A houseguest is named: only houseguests hold grudges.");
            return named[0];
        }

        private static GrudgeState Row(EpisodeState s, string holderId, string targetId) =>
            s.story.grudges.SingleOrDefault(g => g.holderId == holderId && g.targetId == targetId);

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
            // Agency weighs threat as well (NPC-AGENCY-PLAN.md §5.1); off here, so the windows' and
            // the story's terms show on their own.
            s.agencyRulesStartWeek = 0;
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
