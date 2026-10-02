using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Gamesim.Simulation;
using Newtonsoft.Json;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// The commitment rules (ACTIONS-DEALS-ALLIANCES-PLAN R0 and C0, schema 22). Each defect is shown
    /// twice: as it plays without the rules - which is how every recorded season plays, and how
    /// main played everything - and as it plays under them. Unity-free, so the dotnet subset runs it
    /// (Tools/SimulationTests).
    /// </summary>
    public sealed class CommitmentRulesTests
    {
        // ------------------------------------------------------------ the boundary

        [Test]
        public void ASeasonBuiltDirectlyPlaysWithoutTheRulesAndTheirStartIsNeverPastNextWeek()
        {
            var s = Season(3);
            Assert.That(s.schemaVersion, Is.EqualTo(22));
            Assert.That(s.commitmentRulesStartWeek, Is.Zero, "A season a test builds plays without them, as every recorded one does.");
            Assert.That(EpisodeEngine.CommitmentRulesOn(s), Is.False);
            EpisodeEngine.EnableCommitments(s);
            Assert.That(s.commitmentRulesStartWeek, Is.EqualTo(1));
            Assert.That(EpisodeEngine.CommitmentRulesOn(s), Is.True);
            EpisodeEngine.EnableCommitments(s, 9);
            Assert.That(s.commitmentRulesStartWeek, Is.EqualTo(2), "No further off than the week after the season's own.");
            Assert.That(EpisodeEngine.CommitmentRulesOn(s), Is.False, "The week the season is in plays as it was.");
            Assert.That(EpisodeValidation.TryValidate(s, out var error), Is.True, error);
            s.commitmentRulesStartWeek = 3;
            Assert.That(EpisodeValidation.TryValidate(s, out error), Is.False);
            Assert.That(error, Does.Contain("Commitment-rules activation week"));
        }

        // ------------------------------------------------------------ X1: study

        [Test]
        public void X1_UnderTheRulesAStudyIsOneOfTheWindowsActions()
        {
            // Without the rules the week's windows never charged a study: five in one free time, five free points.
            var free = Season(17, 8);
            EpisodeEngine.EnableWeek(free);
            var legacy = new EpisodeEngine(free);
            for (int i = 0; i < 5; i++)
            {
                var studied = Apply(legacy, EpisodeCommandKind.StudyHouse, "memorize-layout");
                Assert.That(studied.accepted, Is.True, "study " + i + ": " + studied.reason);
            }
            Assert.That(legacy.Snapshot.playerStudyBonus, Is.EqualTo(5));
            Assert.That(EpisodeEngine.SocialActionsSpent(legacy.Snapshot), Is.Zero, "A season without the rules keeps its free studies.");

            var s = Season(17, 8);
            EpisodeEngine.EnableWeek(s);
            EpisodeEngine.EnableCommitments(s);
            var engine = new EpisodeEngine(s);
            Assert.That(EpisodeEngine.SocialActionBudget(s), Is.EqualTo(1), "Move-in night seats one in a house of eight.");
            var first = Apply(engine, EpisodeCommandKind.StudyHouse, "memorize-layout");
            Assert.That(first.accepted, Is.True, first.reason);
            Assert.That(EpisodeEngine.SocialActionsSpent(engine.Snapshot), Is.EqualTo(1), "The study spent the window's action.");
            Assert.That(engine.Snapshot.windowActions[Windows.AfterEviction], Is.EqualTo(1));
            Assert.That(engine.Snapshot.events.Last().text, Does.EndWith("One social action spent."), "What the diary always said it cost.");
            var second = Apply(engine, EpisodeCommandKind.StudyHouse, "memorize-layout");
            Assert.That(second.accepted, Is.False, "The window is spent.");
            Assert.That(second.reason, Is.EqualTo("This social window is complete. Continue the episode."));
            Assert.That(engine.Snapshot.playerStudyBonus, Is.EqualTo(1));
        }

        [Test]
        public void X1_UnderTheRulesBoughtTimeIsSpentOnStudyLikeOnAnyOtherAction()
        {
            var s = Season(18, 8);
            EpisodeEngine.EnableWeek(s);
            EpisodeEngine.EnableCommitments(s);
            var engine = new EpisodeEngine(s);
            var bought = Apply(engine, EpisodeCommandKind.BuyActionPoint, null, null, WebSocialVocabulary.SpreadAll);
            Assert.That(bought.accepted, Is.True, bought.reason);
            Assert.That(EpisodeEngine.SocialActionBudget(engine.Snapshot), Is.EqualTo(2));
            Assert.That(Apply(engine, EpisodeCommandKind.StudyHouse, "memorize-layout").accepted, Is.True);
            Assert.That(Apply(engine, EpisodeCommandKind.StudyHouse, "sneak-peek").accepted, Is.True);
            Assert.That(EpisodeEngine.SocialActionsSpent(engine.Snapshot), Is.EqualTo(2));
            Assert.That(Apply(engine, EpisodeCommandKind.SmallTalk, Npcs(s)[0].id).accepted, Is.False, "Nothing is left for a conversation.");
        }

        // ------------------------------------------------------------ X6: a hunt told to the player

        [Test]
        public void X6_UnderTheRulesAPactMatesHuntLeavesThePlayersViewOfTheThreatAlone()
        {
            foreach (bool rules in new[] { false, true })
            {
                var s = HuntToThePlayer(rules, out string hunter, out string threat);
                double before = s.Score(s.playerId, threat);
                Assert.That(Pursue(s, hunter), Is.True);
                Assert.That(s.events.Last().text, Is.EqualTo(s.Find(hunter).name + " told you " + s.Find(threat).name + " has to go."),
                    "Either way the player hears it.");
                Assert.That(s.events.Last().audienceIds, Is.EqualTo(new[] { s.playerId }));
                NpcAlliances.Dissolve(s);
                bool pactStands = s.alliances.Single(a => a.id == "alliance-threat").active;
                if (rules)
                {
                    Assert.That(s.Score(s.playerId, threat), Is.EqualTo(before), "Under the rules the player's view is their own.");
                    Assert.That(pactStands, Is.True, "and their pact with the threat stands.");
                }
                else
                {
                    Assert.That(s.Score(s.playerId, threat), Is.EqualTo(before + EpisodeEngine.HuntImpact), "Without them the hunt wrote into the player's view.");
                    Assert.That(pactStands, Is.False, "which ended their pact with the threat without a word.");
                }
            }
        }

        // ------------------------------------------------------------ X7: a whisper

        [Test]
        public void X7_UnderTheRulesAWhisperReachesThePersonThePlayerIsTalkingTo()
        {
            int reached = 0;
            for (uint seed = 1; seed <= 30; seed++)
            {
                var s = Season(seed, 8);
                EpisodeEngine.EnableCommitments(s);
                var npcs = Npcs(s);
                string subject = npcs[0].id, listener = npcs[5].id;
                var engine = new EpisodeEngine(s);
                var said = Apply(engine, EpisodeCommandKind.SpreadRumor, subject, listener, EpisodeEngine.WhisperCampaign);
                Assert.That(said.accepted, Is.True, said.reason);
                var after = engine.Snapshot;
                var line = after.events.Last();
                if (line.kind == "rumour-backfire") continue;
                reached++;
                Assert.That(line.text, Is.EqualTo("You whispered about " + npcs[0].name + " to " + npcs[5].name + "."));
                Assert.That(after.Score(listener, subject), Is.LessThan(s.Score(listener, subject)), "The one told thinks less of them.");
                foreach (var other in npcs.Skip(1).Where(n => n.id != listener))
                    Assert.That(after.Score(other.id, subject), Is.EqualTo(s.Score(other.id, subject)), other.name + " was not told.");
            }
            Assert.That(reached, Is.GreaterThan(5), "A whisper lands about two times in three.");

            var nobody = Season(4, 8);
            EpisodeEngine.EnableCommitments(nobody);
            var refused = new EpisodeEngine(nobody).Apply(Command(nobody, EpisodeCommandKind.SpreadRumor, Npcs(nobody)[0].id, null, EpisodeEngine.WhisperCampaign));
            Assert.That(refused.accepted, Is.False);
            Assert.That(refused.reason, Is.EqualTo("Choose who to whisper it to."));
        }

        [Test]
        public void X7_WithoutTheRulesTheHouseStillDrawsTheListenerWhoeverIsNamed()
        {
            // The conversation now names the person the player is talking to. A season without the
            // rules ignores the name and draws exactly as it always did.
            bool somebodyElse = false;
            for (uint seed = 1; seed <= 30; seed++)
            {
                var named = Season(seed, 8);
                var unnamed = Season(seed, 8);
                var npcs = Npcs(named);
                var a = new EpisodeEngine(named).Apply(Command(named, EpisodeCommandKind.SpreadRumor, npcs[0].id, npcs[5].id, EpisodeEngine.WhisperCampaign));
                var b = new EpisodeEngine(unnamed).Apply(Command(unnamed, EpisodeCommandKind.SpreadRumor, npcs[0].id, null, EpisodeEngine.WhisperCampaign));
                Assert.That(a.accepted && b.accepted, Is.True, a.reason + " / " + b.reason);
                Assert.That(a.state.randomState, Is.EqualTo(b.state.randomState), "seed " + seed);
                Assert.That(Json(a.state.relationships), Is.EqualTo(Json(b.state.relationships)), "seed " + seed);
                Assert.That(Json(a.state.events), Is.EqualTo(Json(b.state.events)), "seed " + seed);
                var line = a.state.events.Last();
                if (line.kind == "rumour" && !line.text.EndsWith(" to " + npcs[5].name + ".", StringComparison.Ordinal)) somebodyElse = true;
            }
            Assert.That(somebodyElse, Is.True, "Without the rules the one named is not who hears it.");
        }

        // ------------------------------------------------------------ X8: a rumour told to the player

        [Test]
        public void X8_UnderTheRulesARumourToThePlayerSaysWhatWasSaidAndMovesOnlyTheirView()
        {
            foreach (bool rules in new[] { false, true })
            {
                var s = RumourToThePlayer(rules, out string teller, out string subject);
                double mine = s.Score(s.playerId, subject), theirs = s.Score(subject, s.playerId);
                int memories = s.memories.Count(m => m.ownerId == s.playerId);
                Assert.That(NpcSocialActions.Perform(s, teller, NpcActionKind.SpreadInfo), Is.True);
                var line = s.events.Last();
                Assert.That(line.kind, Is.EqualTo("information"));
                Assert.That(line.audienceIds, Is.EqualTo(new[] { s.playerId }));
                Assert.That(s.Score(s.playerId, subject), Is.EqualTo(mine + NpcSocialActions.RumorImpact), "Either way the player thinks less of them.");
                if (rules)
                {
                    Assert.That(line.text, Is.EqualTo(s.Find(teller).name + " told you " + s.Find(subject).name + " is the biggest threat in this house."));
                    Assert.That(s.Score(subject, s.playerId), Is.EqualTo(theirs), "The one it was about heard nothing.");
                    var memory = s.memories.Last(m => m.ownerId == s.playerId);
                    Assert.That(memory.subjectId, Is.EqualTo(subject));
                    Assert.That(memory.text, Does.StartWith(s.Find(teller).name + " told me in week 1 that "));
                    Assert.That(s.memories.Count(m => m.ownerId == s.playerId), Is.EqualTo(memories + 1));
                }
                else
                {
                    Assert.That(line.text, Is.EqualTo(s.Find(teller).name + " told you something about " + s.Find(subject).name + "."));
                    Assert.That(s.Score(subject, s.playerId), Is.Not.EqualTo(theirs), "Without the rules the subject's view of the player moved as well.");
                }
            }
        }

        // ------------------------------------------------------------ a promise to evict made to the one it names

        [Test]
        public void UnderTheRulesAPromiseToEvictIsNeverMadeToThePersonItNames()
        {
            foreach (bool rules in new[] { false, true })
            {
                var s = Campaign(rules);
                string nominee = s.nominees[0];
                var made = new EpisodeEngine(s).Apply(Command(s, EpisodeCommandKind.PromiseVote, nominee, nominee));
                if (rules)
                {
                    Assert.That(made.accepted, Is.False);
                    Assert.That(made.reason, Is.EqualTo("You cannot promise somebody that you will vote them out."));
                }
                else Assert.That(made.accepted, Is.True, "A season without the rules still takes it: " + made.reason);
                var other = new EpisodeEngine(s).Apply(Command(s, EpisodeCommandKind.PromiseVote, s.nominees[1], nominee));
                Assert.That(other.accepted, Is.True, "A promise to evict somebody else is made as it always was: " + other.reason);
            }
        }

        // ------------------------------------------------------------ C0: who broke it, through the engine

        [Test]
        public void C0_UnderTheRulesASettledDealAndPromiseSayWhoBrokeThemAndWhen()
        {
            foreach (bool rules in new[] { false, true })
            {
                var after = NominatedByAPartner(rules, out string hoh);
                var deal = after.deals.Single(d => d.id == "deal-pact");
                var promise = after.promises.Single(p => p.id == "promise-pact");
                Assert.That(after.nominees, Does.Contain(after.playerId), "The Head of Household put the player up.");
                Assert.That(deal.status, Is.EqualTo(DealStatus.Broken));
                Assert.That(promise.status, Is.EqualTo(PromiseStatus.Broken));
                Assert.That(EpisodeValidation.TryValidate(after, out var error), Is.True, error);
                if (rules)
                {
                    Assert.That(deal.brokenById, Is.EqualTo(hoh));
                    Assert.That(deal.settledWeek, Is.EqualTo(after.week));
                    Assert.That(promise.brokenById, Is.EqualTo(hoh));
                    Assert.That(promise.settledWeek, Is.EqualTo(after.week));
                }
                else
                {
                    Assert.That(deal.brokenById, Is.Null, "A season without the rules writes none of their records.");
                    Assert.That(deal.settledWeek, Is.Zero);
                    Assert.That(promise.brokenById, Is.Null);
                    Assert.That(promise.settledWeek, Is.Zero);
                }
            }
        }

        [Test]
        public void C0_UnderTheRulesTheVictimOfABreachIsNeverHeldToIt()
        {
            foreach (bool rules in new[] { false, true })
            {
                var after = NominatedByAPartner(rules, out string hoh);
                string player = after.playerId;
                string bystander = Npcs(after).First(n => n.id != hoh).id;
                if (rules)
                {
                    Assert.That(NpcDeals.BrokenDeals(after, player), Is.Zero, "warmth: the player broke nothing.");
                    Assert.That(NpcDeals.BrokenDeals(after, hoh), Is.EqualTo(1), "The one who broke it is held to it.");
                    Assert.That(NpcDeals.Adjusted(after, bystander, player), Is.EqualTo(after.Score(bystander, player)));
                    Assert.That(ThreatAssessment.TrustScore(after, player, hoh), Is.EqualTo(ThreatAssessment.NeutralTrust),
                        "threat: the one who broke it holds nothing against the one they wronged.");
                    Assert.That(StoryOdds.PlayerBrokeTheirWord(after, hoh), Is.False, "The story's odds do not say the player broke their word.");
                    Assert.That(PlayerDeals.AcceptanceChance(after, bystander, DealKind.Partnership, null),
                        Is.EqualTo(PlayerDeals.AcceptanceChance(Without(after, "deal-pact"), bystander, DealKind.Partnership, null)), "acceptance");
                }
                else
                {
                    Assert.That(NpcDeals.BrokenDeals(after, player), Is.EqualTo(1), "Without the rules the victim is held to it too.");
                    Assert.That(ThreatAssessment.TrustScore(after, player, hoh), Is.LessThan(ThreatAssessment.NeutralTrust));
                    Assert.That(StoryOdds.PlayerBrokeTheirWord(after, hoh), Is.True, "and the story said the player broke their word.");
                    Assert.That(PlayerDeals.AcceptanceChance(after, bystander, DealKind.Partnership, null),
                        Is.LessThan(PlayerDeals.AcceptanceChance(Without(after, "deal-pact"), bystander, DealKind.Partnership, null)));
                }
            }
        }

        // ------------------------------------------------------------ X11: breaches do not fade

        [Test]
        public void X11_UnderTheRulesADealBrokenNeverFadesAndAPromisesOutcomeIsOnTheRecord()
        {
            foreach (bool rules in new[] { false, true })
            {
                var after = NominatedByAPartner(rules, out string hoh);
                string player = after.playerId;
                var held = Entries(after, player, hoh, "deal_broken");
                Assert.That(held, Has.Count.EqualTo(1), "The one wronged holds it.");
                var promised = Entries(after, player, hoh, "promise-broken");
                if (rules)
                {
                    Assert.That(held[0].decayable, Is.False);
                    Assert.That(RelationshipLedger.Weight(held[0], after.week + 10), Is.EqualTo(1), "Ten weeks on, still in full.");
                    var kept = Entries(after, hoh, player, "deal_broken");
                    Assert.That(kept.Single().impactScore, Is.Zero, "The one who broke it keeps a record of it, weighing nothing.");
                    Assert.That(promised, Has.Count.EqualTo(1), "A promise's outcome is on the record.");
                    Assert.That(promised[0].decayable, Is.False);
                    Assert.That(promised[0].impactScore, Is.EqualTo(WebRules.PromiseImpact(PromiseKind.Safety, PromiseStatus.Broken)));
                    Assert.That(Entries(after, hoh, player, "promise-broken"), Is.Empty, "One way: the promiser holds nothing against the promisee.");
                }
                else
                {
                    Assert.That(held[0].decayable, Is.True, "Without the rules a breach faded, as it still does in those seasons.");
                    Assert.That(RelationshipLedger.Weight(held[0], after.week + 10), Is.Zero);
                    Assert.That(Entries(after, hoh, player, "deal_broken").Single().impactScore, Is.EqualTo(held[0].impactScore), "and was held both ways.");
                    Assert.That(promised, Is.Empty, "and no promise's outcome was recorded.");
                }
            }
        }

        // ------------------------------------------------------------ the other readers, on a record

        [Test]
        public void C0_TheAcceptanceLineAndTheShownOddsHoldOnlyThePlayersOwnBreaches()
        {
            foreach (bool rules in new[] { false, true })
            {
                var s = Season(41, 8);
                if (rules) EpisodeEngine.EnableCommitments(s);
                var npcs = Npcs(s);
                string asked = npcs[3].id;
                double clean = PlayerDeals.AcceptanceChance(s, asked, DealKind.Partnership, null);
                double shownClean = KnownOdds.Deal(s, asked, DealKind.Partnership, null).chance;
                // Two deals broken against the player, by the ones they were with.
                s.deals.Add(Broken(s, "theirs-1", npcs[0].id, npcs[0].id, rules));
                s.deals.Add(Broken(s, "theirs-2", npcs[1].id, npcs[1].id, rules));
                string line = PlayerDeals.Reasoning(s, asked, DealKind.Partnership, false);
                if (rules)
                {
                    Assert.That(line, Is.Not.EqualTo("I've heard you've broken deals before. I can't trust that."));
                    Assert.That(PlayerDeals.AcceptanceChance(s, asked, DealKind.Partnership, null), Is.EqualTo(clean));
                    Assert.That(KnownOdds.Deal(s, asked, DealKind.Partnership, null).chance, Is.EqualTo(shownClean));
                }
                else
                {
                    Assert.That(line, Is.EqualTo("I've heard you've broken deals before. I can't trust that."), "Without the rules it was said to the one wronged.");
                    Assert.That(PlayerDeals.AcceptanceChance(s, asked, DealKind.Partnership, null), Is.LessThan(clean), "and the roll held it against them.");
                }
                // Two the player broke count either way.
                var own = Season(41, 8);
                if (rules) EpisodeEngine.EnableCommitments(own);
                own.deals.Add(Broken(own, "mine-1", npcs[0].id, own.playerId, rules));
                own.deals.Add(Broken(own, "mine-2", npcs[1].id, own.playerId, rules));
                Assert.That(PlayerDeals.Reasoning(own, asked, DealKind.Partnership, false), Is.EqualTo("I've heard you've broken deals before. I can't trust that."));
                Assert.That(KnownOdds.Deal(own, asked, DealKind.Partnership, null).chance, Is.LessThan(shownClean));
            }
        }

        [Test]
        public void C0_ThreatHoldsOnlyThePromisesSomebodyBroke()
        {
            foreach (bool rules in new[] { false, true })
            {
                var s = Season(42, 8);
                if (rules) EpisodeEngine.EnableCommitments(s);
                var npcs = Npcs(s);
                string evaluator = npcs[4].id;
                double before = ThreatAssessment.Total(s, evaluator, s.playerId);
                // Two promises broken to the player.
                s.promises.Add(BrokenPromise(s, "to-you-1", npcs[0].id, s.playerId, rules));
                s.promises.Add(BrokenPromise(s, "to-you-2", npcs[1].id, s.playerId, rules));
                double after = ThreatAssessment.Total(s, evaluator, s.playerId);
                if (rules) Assert.That(after, Is.EqualTo(before), "Being lied to does not make the player a threat.");
                else Assert.That(after, Is.EqualTo(before + 6), "Without the rules it did.");
                Assert.That(ThreatAssessment.Total(s, evaluator, npcs[0].id), Is.GreaterThan(ThreatAssessment.Total(Without(s, "to-you-1"), evaluator, npcs[0].id)),
                    "The one who broke it reads as the danger either way.");
            }
        }

        [Test]
        public void C0_AJurorWhoBrokeTheirWordDoesNotHoldItAgainstTheFinalist()
        {
            foreach (bool rules in new[] { false, true })
            {
                var s = Season(43, 8);
                if (rules) EpisodeEngine.EnableCommitments(s);
                var npcs = Npcs(s);
                string juror = npcs[0].id, finalist = npcs[1].id;
                s.deals.Add(Broken(s, "jurors-deal", juror, juror, rules, other: finalist));
                s.promises.Add(BrokenPromise(s, "jurors-promise", juror, finalist, rules));
                double obligations = WebJuryVoting.Obligations(s, juror, finalist);
                if (rules) Assert.That(obligations, Is.Zero, "The juror broke both: neither counts against the finalist.");
                else Assert.That(obligations, Is.EqualTo(-50), "Without the rules both counted against the finalist they wronged.");
                var theirs = Season(43, 8);
                if (rules) EpisodeEngine.EnableCommitments(theirs);
                theirs.deals.Add(Broken(theirs, "finalists-deal", juror, finalist, rules, other: finalist));
                Assert.That(WebJuryVoting.Obligations(theirs, juror, finalist), Is.EqualTo(-50), "A breach of the finalist's own counts either way.");
            }
        }

        [Test]
        public void C0_GameSenseCostsTheVictimOfABrokenDealNothing()
        {
            foreach (bool rules in new[] { false, true })
            {
                var s = Season(44, 8);
                if (rules) EpisodeEngine.EnableCommitments(s);
                var npcs = Npcs(s);
                s.deals.Add(Broken(s, "deal-theirs", npcs[0].id, npcs[0].id, rules));
                EpisodeEngine.ReconcileOpportunities(s);
                var note = GameSense.Evaluate(s).notes.Single(n => n.rowId == "deal-theirs");
                if (rules)
                {
                    Assert.That(note.points, Is.Zero);
                    Assert.That(note.text, Does.Contain("a deal broken against you"));
                }
                else Assert.That(note.points, Is.EqualTo(-5), "Without the rules the player paid for the other side's breach.");

                var own = Season(44, 8);
                if (rules) EpisodeEngine.EnableCommitments(own);
                own.deals.Add(Broken(own, "deal-mine", npcs[0].id, own.playerId, rules));
                EpisodeEngine.ReconcileOpportunities(own);
                Assert.That(GameSense.Evaluate(own).notes.Single(n => n.rowId == "deal-mine").points, Is.EqualTo(-5), "Their own breach costs them either way.");
            }
        }

        [Test]
        public void C0_TheJuryReadCoolsOnlyOnTheDealsThePlayerBrokeAfterTheRead()
        {
            foreach (bool rules in new[] { false, true })
            {
                var s = FinalThree(rules);
                var juror = FinalistRead.Jurors(s)[0].id;
                Read(s, juror, 3, 45);
                s.deals.Add(Broken(s, "their-breach", juror, juror, rules, week: 4));
                var read = JuryHouseRead.ReadJuror(s, juror);
                if (rules) Assert.That(read.band, Is.EqualTo(JuryHouseRead.Supportive), "A deal the juror broke is not the player's to answer for.");
                else
                {
                    Assert.That(read.band, Is.EqualTo(JuryHouseRead.Wavering));
                    Assert.That(read.reason, Does.EndWith("Then a deal between you broke since."));
                }

                var mine = FinalThree(rules);
                Read(mine, juror, 3, 45);
                mine.deals.Add(Broken(mine, "my-breach", juror, mine.playerId, rules, week: 4));
                var cooled = JuryHouseRead.ReadJuror(mine, juror);
                Assert.That(cooled.band, Is.EqualTo(JuryHouseRead.Wavering), "The player's own breach after the read cools it either way.");
                Assert.That(cooled.reason, Does.EndWith(rules ? "Then you broke a deal with them since." : "Then a deal between you broke since."));

                if (!rules) continue;
                // Under the rules the record holds the week it broke: a breach in the read's own week is already in it.
                var sameWeek = FinalThree(true);
                Read(sameWeek, juror, 4, 45);
                sameWeek.deals.Add(Broken(sameWeek, "same-week", juror, sameWeek.playerId, true, week: 4));
                Assert.That(JuryHouseRead.ReadJuror(sameWeek, juror).band, Is.EqualTo(JuryHouseRead.Supportive));
            }
        }

        // ------------------------------------------------------------ a deal settled before the record

        [Test]
        public void ALegacyDealIsReadByTheDealBreakersRuleUnderTheRules()
        {
            var s = Season(45, 8);
            var npcs = Npcs(s);
            string hoh = npcs[0].id;
            s.week = 3;
            // A safety pact the Head of Household broke at week two's nominations, settled before the record existed.
            var legacy = new DealState
            {
                id = "legacy-pact", type = DealKind.SafetyAgreement, proposerId = s.playerId, recipientId = hoh,
                status = DealStatus.Broken, week = 2, expiresWeek = 3, trustImpact = DealKind.DefaultTrust(DealKind.SafetyAgreement),
            };
            s.deals.Add(legacy);
            s.ledger.power.Add(new PowerRow { week = 2, hohId = hoh, nominees = new List<string> { s.playerId, npcs[1].id }, evicteeId = npcs[1].id, tally = new List<int> { 1, 4 } });
            // A voting bloc that fell apart, and a pair's deal the player was never part of.
            var bloc = new DealState { id = "legacy-bloc", type = DealKind.VoteTogether, proposerId = s.playerId, recipientId = npcs[2].id, status = DealStatus.Broken, week = 2, trustImpact = DealTrust.Medium };
            var theirs = new DealState { id = "legacy-theirs", type = DealKind.SafetyAgreement, proposerId = npcs[3].id, recipientId = npcs[4].id, status = DealStatus.Broken, week = 2, trustImpact = DealTrust.High };
            s.deals.Add(bloc); s.deals.Add(theirs);
            EpisodeEngine.EnableCommitments(s, 3);
            Assert.That(EpisodeEngine.CommitmentRulesOn(s), Is.True);

            Assert.That(FinalistRead.DealBreaker(s, legacy), Is.EqualTo(hoh));
            Assert.That(Breaches.DealBreaker(s, legacy), Is.EqualTo(FinalistRead.DealBreaker(s, legacy)), "The record's fallback is DealBreaker's rule.");
            Assert.That(Breaches.CountsAgainst(s, legacy, hoh), Is.True);
            Assert.That(Breaches.CountsAgainst(s, legacy, s.playerId), Is.False, "Its victim is not held to it.");
            Assert.That(Breaches.DealBreaker(s, bloc), Is.Null);
            Assert.That(Breaches.CountsAgainst(s, bloc, s.playerId) && Breaches.CountsAgainst(s, bloc, npcs[2].id), Is.True, "A voting bloc both walked away from.");
            Assert.That(Breaches.DealBreaker(s, theirs), Is.Null, "A pair's deal names nobody the player can read.");
            Assert.That(NpcDeals.BrokenDeals(s, npcs[3].id) + NpcDeals.BrokenDeals(s, npcs[4].id), Is.Zero);

            // The same deals with the record written read by the record.
            legacy.brokenById = s.playerId; legacy.settledWeek = 2;
            Assert.That(Breaches.DealBreaker(s, legacy), Is.EqualTo(s.playerId), "The record wins over the reading.");
            bloc.settledWeek = 2;
            Assert.That(Breaches.DealBreaker(s, bloc), Is.Null);
            Assert.That(Breaches.Broke(s, bloc, s.playerId), Is.True);
        }

        // ------------------------------------------------------------ the records' contract

        [Test]
        public void TheRecordsAreBoundAndOnlyASeasonUnderTheRulesHoldsThem()
        {
            var s = Season(46, 8);
            EpisodeEngine.EnableCommitments(s);
            var npcs = Npcs(s);
            var deal = Broken(s, "d", npcs[0].id, npcs[0].id, true);
            s.deals.Add(deal);
            var promise = BrokenPromise(s, "p", npcs[1].id, s.playerId, true);
            s.promises.Add(promise);
            Valid(s);

            deal.brokenById = npcs[1].id; Invalid(s, "Invalid deal settlement.");
            deal.brokenById = npcs[0].id; deal.status = DealStatus.Fulfilled; Invalid(s, "Invalid deal settlement.");
            deal.brokenById = null; Valid(s);
            deal.settledWeek = 2; Invalid(s, "Invalid deal settlement.");
            deal.settledWeek = 1; deal.status = DealStatus.Active; Invalid(s, "Invalid deal settlement.");
            deal.status = DealStatus.Broken; deal.brokenById = npcs[0].id; Valid(s);

            promise.brokenById = npcs[2].id; Invalid(s, "Invalid promise settlement.");
            promise.brokenById = npcs[1].id; promise.status = PromiseStatus.Expired; Invalid(s, "Invalid promise settlement.");
            promise.status = PromiseStatus.Broken; promise.settledWeek = -1; Invalid(s, "Invalid promise settlement.");
            promise.settledWeek = 1; Valid(s);

            s.commitmentRulesStartWeek = 0;
            Invalid(s, "A season without the commitment rules has none of their records.");
            deal.brokenById = null; deal.settledWeek = 0; promise.brokenById = null; promise.settledWeek = 0;
            Valid(s);
        }

        // ------------------------------------------------------------ fixtures

        private static EpisodeState Season(uint seed, int size = 6) =>
            SeasonBuilder.Create(new SeasonBuilder.Choice { HouseSize = size }, seed);

        private static List<ContestantState> Npcs(EpisodeState s) => s.Active.Where(c => !c.isPlayer).ToList();

        private static void SetScore(EpisodeState s, string from, string to, double score)
        {
            var edge = s.relationships.FirstOrDefault(r => r.fromId == from && r.toId == to);
            if (edge == null) s.relationships.Add(edge = new RelationshipState { fromId = from, toId = to });
            edge.score = score;
        }

        private static string Json(object value) => JsonConvert.SerializeObject(value);

        private static EpisodeCommand Command(EpisodeState s, EpisodeCommandKind kind, string target = null, string second = null, string text = null) =>
            new EpisodeCommand
            {
                id = "commitments-" + kind + "-" + s.revision, actorId = s.playerId, kind = kind,
                targetId = target, secondTargetId = second, text = text, expectedRevision = s.revision, expectedPhase = s.phase,
            };

        private static CommandResult Apply(EpisodeEngine engine, EpisodeCommandKind kind, string target = null, string second = null, string text = null) =>
            engine.Apply(Command(engine.Snapshot, kind, target, second, text));

        private static void Valid(EpisodeState s) => Assert.That(EpisodeValidation.TryValidate(s, out var error), Is.True, error);

        private static void Invalid(EpisodeState s, string reason)
        {
            Assert.That(EpisodeValidation.TryValidate(s, out var error), Is.False);
            Assert.That(error, Is.EqualTo(reason));
        }

        /// <summary>A deal between the player (or <paramref name="other"/>) and <paramref name="with"/>, broken by <paramref name="breaker"/>; recorded as the rules record it, or not at all.</summary>
        private static DealState Broken(EpisodeState s, string id, string with, string breaker, bool recorded, string other = null, int week = 1) =>
            new DealState
            {
                id = id, type = DealKind.SafetyAgreement, proposerId = other ?? s.playerId, recipientId = with,
                status = DealStatus.Broken, week = week, expiresWeek = week + 1, trustImpact = DealKind.DefaultTrust(DealKind.SafetyAgreement),
                brokenById = recorded ? breaker : null, settledWeek = recorded ? week : 0,
            };

        private static PromiseState BrokenPromise(EpisodeState s, string id, string from, string to, bool recorded) =>
            new PromiseState
            {
                id = id, fromId = from, toId = to, kind = PromiseKind.Safety, status = PromiseStatus.Broken, week = 1, expiresWeek = 2,
                brokenById = recorded ? from : null, settledWeek = recorded ? 1 : 0,
            };

        /// <summary>The same season without one deal or promise.</summary>
        private static EpisodeState Without(EpisodeState s, string id)
        {
            var copy = s.Clone();
            copy.deals.RemoveAll(d => d.id == id);
            copy.promises.RemoveAll(p => p.id == id);
            return copy;
        }

        private static List<RelationshipEventState> Entries(EpisodeState s, string from, string to, string type) =>
            s.relationships.Where(r => r.fromId == from && r.toId == to).SelectMany(r => r.events).Where(e => e.type == type).ToList();

        /// <summary>
        /// A Head of Household with a safety pact and a safety promise to the player, who likes
        /// everybody else and puts the player up: the pact and the promise are broken by them.
        /// </summary>
        private static EpisodeState NominatedByAPartner(bool rules, out string hoh, uint seed = 29)
        {
            var s = Season(seed, 8);
            var npcs = Npcs(s);
            string head = npcs[0].id;
            hoh = head;
            s.phase = EpisodePhase.Nomination;
            s.hohId = head;
            s.competitionResolved = true;
            foreach (var other in s.contestants.Where(c => c.id != head)) SetScore(s, head, other.id, other.isPlayer ? -90 : 60);
            s.deals.Add(new DealState
            {
                id = "deal-pact", type = DealKind.SafetyAgreement, proposerId = s.playerId, recipientId = head,
                status = DealStatus.Active, week = 1, expiresWeek = 2, trustImpact = DealKind.DefaultTrust(DealKind.SafetyAgreement),
            });
            s.promises.Add(new PromiseState { id = "promise-pact", fromId = head, toId = s.playerId, kind = PromiseKind.Safety, status = PromiseStatus.Active, week = 1, expiresWeek = 2 });
            if (rules) EpisodeEngine.EnableCommitments(s);
            Valid(s);
            var engine = new EpisodeEngine(s);
            var advanced = Apply(engine, EpisodeCommandKind.Advance);
            Assert.That(advanced.accepted, Is.True, advanced.reason);
            return engine.Snapshot;
        }

        /// <summary>A pact-mate of the player's hunting a houseguest the player is also in a pact with, at a view of them just above the line a pact sours at.</summary>
        private static EpisodeState HuntToThePlayer(bool rules, out string hunter, out string threat)
        {
            var s = Season(23, 6);
            s.agencyRulesStartWeek = 1;
            if (rules) EpisodeEngine.EnableCommitments(s);
            var npcs = Npcs(s);
            hunter = npcs[0].id;
            threat = npcs[1].id;
            s.alliances.Add(new AllianceState { id = "alliance-hunt", name = "The Hunt", members = new List<string> { hunter, s.playerId }, active = true });
            s.alliances.Add(new AllianceState { id = "alliance-threat", name = "The Threat", members = new List<string> { s.playerId, threat }, active = true });
            SetScore(s, hunter, threat, -50);
            SetScore(s, s.playerId, threat, NpcAlliances.SourLine + 2);
            var agenda = NpcAgendas.Of(s, hunter);
            Assert.That(agenda.kind, Is.EqualTo(Agendas.Hunt));
            Assert.That(agenda.partnerId, Is.EqualTo(s.playerId));
            Assert.That(agenda.targetId, Is.EqualTo(threat));
            return s;
        }

        private static bool Pursue(EpisodeState s, string npcId) => (bool)typeof(NpcSocialActions)
            .GetMethod("Pursue", BindingFlags.NonPublic | BindingFlags.Static)
            .Invoke(null, new object[] { s, s.Find(npcId) });

        /// <summary>A house of three where the teller's draw falls on the player, and the subject is the third.</summary>
        private static EpisodeState RumourToThePlayer(bool rules, out string teller, out string subject)
        {
            for (uint roll = 1; roll < 64; roll++)
            {
                var s = Season(47, 3);
                if (rules) EpisodeEngine.EnableCommitments(s);
                var npcs = Npcs(s);
                teller = npcs[0].id;
                subject = npcs[1].id;
                SetScore(s, teller, s.playerId, 100);
                SetScore(s, teller, subject, -100);
                s.randomState = roll * 2654435761u;
                var probe = s.Clone();
                NpcSocialActions.Perform(probe, teller, NpcActionKind.SpreadInfo);
                if (probe.events.Count > s.events.Count && probe.events.Last().kind == "information"
                    && probe.events.Last().audienceIds.SequenceEqual(new[] { s.playerId })) return s;
            }
            Assert.Fail("No roll drew the player as the listener.");
            teller = subject = null;
            return null;
        }

        /// <summary>A campaign with the player voting and two houseguests on the block.</summary>
        private static EpisodeState Campaign(bool rules)
        {
            var s = ContentCatalog.Create(7);
            s.strategyRulesStartWeek = 1;
            var npcs = s.Active.Where(c => !c.isPlayer).Select(c => c.id).ToList();
            s.phase = EpisodePhase.Campaign;
            s.hohId = npcs[0];
            s.nominees = new List<string> { npcs[1], npcs[2] };
            s.vetoHolderId = npcs[3];
            s.vetoPlayers = s.Active.Select(c => c.id).Take(EpisodeEngine.VetoPlayerCount(s.Active.Count())).ToList();
            if (!s.vetoPlayers.Contains(s.vetoHolderId)) s.vetoPlayers[s.vetoPlayers.Count - 1] = s.vetoHolderId;
            s.vetoResolved = true;
            if (rules) EpisodeEngine.EnableCommitments(s);
            Valid(s);
            return s;
        }

        /// <summary>The Final 3's window: the player and two finalists, three jurors.</summary>
        private static EpisodeState FinalThree(bool rules)
        {
            var s = SeasonBuilder.Create(new SeasonBuilder.Choice { HouseSize = 6 }, 41);
            s.week = 8;
            foreach (var juror in s.contestants.Where(c => !c.isPlayer).Take(3)) juror.status = ContestantStatus.Jury;
            s.phase = EpisodePhase.Social;
            if (rules) EpisodeEngine.EnableCommitments(s);
            return s;
        }

        private static void Read(EpisodeState s, string jurorId, int week, double score) =>
            s.ledger.standings.Add(new StandingRow { week = week, fromId = jurorId, toId = s.playerId, source = ClaimSource.Read, score = score });
    }
}
