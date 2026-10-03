using System.Collections.Generic;
using System.Linq;
using Gamesim.Simulation;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// Your word in the house (ACTIONS-DEALS-ALLIANCES-PLAN C8): a deal the player breaks in front of
    /// the house becomes a fact the two of them know, which the house's own gossip carries; every
    /// houseguest it reaches thinks less of the player and is named to them in a line; the public
    /// reading built from those facts alone is a term of the acceptance roll and of the odds the
    /// player sees, and the words the player is told name the moves that take it and those that strike
    /// deals without it; a breach a ballot decided is never house knowledge; and a full list of facts
    /// never drops an alliance's (X14) or the player's broken word. Each half against the season without
    /// the rules, which plays as it always did. Unity-free, so the dotnet subset runs it
    /// (Tools/SimulationTests).
    /// </summary>
    public sealed class YourWordInTheHouseTests
    {
        private const string VetoDealId = "deal-veto-word";

        // ------------------------------------------------------------ the fact

        [Test]
        public void ADealBrokenInFrontOfTheHouseIsAFactTheTwoOfThemKnowAtFirst()
        {
            var after = VetoKeptFromAPartner(true, true, out string partner);
            var deal = after.deals.Single(d => d.id == VetoDealId);
            Assert.That((deal.status, deal.brokenById), Is.EqualTo((DealStatus.Broken, after.playerId)),
                "Keeping the block broke the player's word on the veto.");
            var fact = Knowledge.Of(after, FactKinds.BrokenWord, VetoDealId);
            Assert.That(fact, Is.Not.Null, "The breach is house knowledge, found by the deal's id.");
            Assert.That(fact.actorId, Is.EqualTo(after.playerId), "The player broke it,");
            Assert.That(fact.subjectId, Is.EqualTo(partner), "against the one they gave their word to,");
            Assert.That(fact.visibility, Is.EqualTo(FactVisibility.Whispered), "out as a whisper the house's gossip carries,");
            Assert.That(fact.knowers, Is.EquivalentTo(new[] { after.playerId, partner }), "and known at first to the two of them and nobody else.");
            Assert.That(fact.week, Is.EqualTo(after.week));
            Assert.That(after.story.facts.Count(f => f.kind == FactKinds.BrokenWord), Is.EqualTo(1), "One fact a deal.");
            Assert.That(YourWord.Breaches(after).Select(f => f.id), Is.EqualTo(new[] { fact.id }));
            Assert.That(YourWord.HeardBy(after, fact), Is.EqualTo(new[] { partner }), "The one wronged has heard of it.");
            Assert.That(YourWord.Word(after), Is.EqualTo(YourWord.Questioned));
            Assert.That(HeardOfThePlayer(after), Is.Empty, "Nobody heard of it all at once and in silence.");
            Assert.That(after.events.Last(e => e.kind == "deal-outcome").audienceIds, Is.EquivalentTo(new[] { after.playerId, partner }),
                "The settlement told the player the breach that moved their reading.");
            Valid(after);
        }

        [Test]
        public void ADealBrokenByThePlayersBallotIsNeverHouseKnowledgeAndCostsNothingAtTheTable()
        {
            foreach (bool story in new[] { false, true })
            {
                var after = PartnerVotedOut(story, out string partner, out string bystander);
                var deal = after.deals.Single(d => d.id == "deal-partners");
                Assert.That((deal.status, deal.brokenById), Is.EqualTo((DealStatus.Broken, after.playerId)), "The player voted their partner out.");
                Assert.That(KnownBallots.SettledByABallot(deal), Is.True);
                Assert.That(after.story.facts.Any(f => f.kind == FactKinds.BrokenWord), Is.False, "A ballot's breach writes no fact: the house votes in secret.");
                Assert.That(after.events.Any(IsAHearingLine), Is.False);
                var clean = Without(after, "deal-partners");
                if (story)
                {
                    Assert.That(HeardOfThePlayer(after), Is.Empty, "and nobody hears of it, in silence or otherwise.");
                    Assert.That((YourWord.Cost(after), PlayerDeals.WordPenalty(after)), Is.EqualTo((0.0, 0.0)), "The reading never reads it,");
                    Assert.That(PlayerDeals.AcceptanceChance(after, bystander, DealKind.Partnership, null),
                        Is.EqualTo(PlayerDeals.AcceptanceChance(clean, bystander, DealKind.Partnership, null)), "nor does the roll,");
                    Assert.That(KnownOdds.Deal(after, bystander, DealKind.Partnership, null).chance,
                        Is.EqualTo(KnownOdds.Deal(clean, bystander, DealKind.Partnership, null).chance), "nor the odds the player sees.");
                }
                else
                {
                    Assert.That(PlayerDeals.WordPenalty(after), Is.EqualTo(PlayerDeals.BrokenDealPenalty),
                        "Without the house's knowledge every breach of the player's counts at the table, as C0 has it.");
                    Assert.That(PlayerDeals.AcceptanceChance(after, bystander, DealKind.Partnership, null),
                        Is.LessThan(PlayerDeals.AcceptanceChance(clean, bystander, DealKind.Partnership, null)));
                }
            }
        }

        // ------------------------------------------------------------ the spread

        [Test]
        public void TheHousesGossipCarriesItAsItCarriesEveryFact()
        {
            var s = Season(31, 8);
            WithTheHousesKnowledge(s);
            var npcs = Npcs(s);
            string wronged = npcs[0].id, warmest = npcs[1].id;
            var fact = BreakInFrontOfTheHouse(s, "deal-spread", wronged);
            // The one wronged is warmest on one houseguest, in a pact with them - and warmer still on the player, who knows.
            foreach (var other in npcs.Where(n => n.id != wronged)) SetScore(s, wronged, other.id, other.id == warmest ? 90 : 10);
            SetScore(s, wronged, s.playerId, 100);
            s.alliances.Add(new AllianceState { id = "alliance-gossip", name = "The Gossip", members = new List<string> { wronged, warmest }, active = true });
            var told = new List<(HouseFactState fact, string listener)>();
            for (int pass = 0; pass < 64 && told.Count == 0; pass++)
                told = Knowledge.Spread(s, "probe-" + pass).Where(t => t.fact.id == fact.id).ToList();
            Assert.That(told, Has.Count.EqualTo(1), "One new knower a pass, as every fact spreads.");
            Assert.That(told[0].listener, Is.EqualTo(warmest),
                "The one who knew it told their warmest houseguest who did not - never the player, who knew it already.");
            Assert.That(fact.knowers, Is.EquivalentTo(new[] { s.playerId, wronged, warmest }));

            // Only a houseguest tells: a fact the player alone knows goes nowhere.
            var mine = BreakInFrontOfTheHouse(s, "deal-alone", npcs[2].id);
            mine.knowers.Remove(npcs[2].id);
            for (int pass = 0; pass < 64; pass++)
                Assert.That(Knowledge.Spread(s, "alone-" + pass).Any(t => t.fact.id == mine.id), Is.False, "The player is never a teller.");
        }

        [Test]
        public void EveryHouseguestTheGossipReachesIsNamedToThePlayerAndThinksLessOfThem()
        {
            int hearings = 0;
            for (uint seed = 29; seed < 41 && hearings < 3; seed++)
            {
                var engine = new EpisodeEngine(VetoKeptFromAPartner(true, true, out _, seed));
                for (int step = 0; step < 80 && engine.Snapshot.phase != EpisodePhase.Finished; step++)
                {
                    var before = engine.Snapshot;
                    var result = engine.Apply(EpisodeEngineTests.NextCommand(before));
                    Assert.That(result.accepted, Is.True, "seed " + seed + ": " + result.reason);
                    var after = engine.Snapshot;
                    var fact = Knowledge.Of(after, FactKinds.BrokenWord, VetoDealId);
                    Assert.That(fact, Is.Not.Null, "The house never forgets it.");
                    var told = fact.knowers.Except(Knowledge.Of(before, FactKinds.BrokenWord, VetoDealId).knowers).ToList();
                    var lines = after.events.Where(e => e.sequence >= before.nextSequence && IsAHearingLine(e)).ToList();
                    Assert.That(lines.Select(e => e.text), Is.EquivalentTo(told.Select(id => YourWord.HeardLine(after, fact, id))),
                        "seed " + seed + ": each houseguest the gossip told is named to the player, and nobody else is.");
                    foreach (var line in lines)
                    {
                        Assert.That(line.kind, Is.EqualTo(StoryLog.Whisper), "A whisper,");
                        Assert.That(line.audienceIds, Is.EqualTo(new[] { after.playerId }), "to the player alone.");
                    }
                    foreach (string id in told)
                    {
                        Assert.That(after.Find(id).isPlayer, Is.False);
                        var entry = NewEntries(before, after, id, after.playerId, YourWord.HeardType);
                        Assert.That(entry, Has.Count.EqualTo(1), "seed " + seed + ": " + id + " holds what they heard against the player,");
                        Assert.That(entry[0].impactScore, Is.EqualTo(YourWord.HeardImpact), "at the hearing's weight,");
                        Assert.That(after.relationships.Single(r => r.fromId == id && r.toId == after.playerId).notes, Does.Contain(entry[0].description),
                            "on their view of the player.");
                        hearings++;
                    }
                    var silent = after.contestants.Where(c => !c.isPlayer && !told.Contains(c.id)
                        && NewEntries(before, after, c.id, after.playerId, YourWord.HeardType).Count > 0).Select(c => c.id).ToList();
                    Assert.That(silent, Is.Empty, "seed " + seed + ": nobody holds a hearing of it against the player without being named to them.");
                }
            }
            Assert.That(hearings, Is.GreaterThan(0), "The gossip carried it.");
        }

        // ------------------------------------------------------------ the reading

        [Test]
        public void TheReadingReadsOnlyTheBreachesTheHouseSawAndIsBounded()
        {
            var s = Season(37, 8);
            WithTheHousesKnowledge(s);
            var npcs = Npcs(s);
            Assert.That(YourWord.On(s), Is.True);
            Assert.That((YourWord.Hearings(s), YourWord.Cost(s), YourWord.Word(s)), Is.EqualTo((0, 0.0, YourWord.Good)));
            Assert.That(YourWord.Summary(s), Is.EqualTo("The house has heard of no deal you broke in front of it."));
            Assert.That(YourWord.OddsLine(s), Is.Null, "Nothing to say beside the odds while nobody has heard.");

            // A breach by the player's ballot is on the record and nowhere in the reading.
            s.deals.Add(BrokenByThePlayer(s, "deal-by-ballot", DealKind.Partnership, npcs[0].id));
            Assert.That(NpcDeals.BrokenDeals(s, s.playerId), Is.EqualTo(1), "The record holds it against the player,");
            Assert.That(YourWord.Cost(s), Is.Zero, "but the house never saw it.");

            // In front of the house, the one wronged knows; each houseguest who hears of it after is a hearing.
            var first = BreakInFrontOfTheHouse(s, "deal-first", npcs[1].id);
            Assert.That((YourWord.Hearings(s), YourWord.Cost(s), YourWord.Word(s)), Is.EqualTo((1, YourWord.PerHearing, YourWord.Questioned)));
            // Somebody who heard and has since left the house still heard: nobody's knowledge leaves with them.
            s.Find(npcs[1].id).status = ContestantStatus.Jury;
            Assert.That(YourWord.Hearings(s), Is.EqualTo(1), "The one wronged has gone to the jury, and still heard of it.");
            s.Find(npcs[1].id).status = ContestantStatus.Active;
            foreach (var other in npcs.Skip(2).Take(5)) Knowledge.AddKnower(s, first, other.id);
            Assert.That(YourWord.HeardBy(s, first), Has.Count.EqualTo(YourWord.WholeHouse));
            Assert.That((YourWord.Cost(s), YourWord.Word(s)), Is.EqualTo((PlayerDeals.BrokenDealPenalty, YourWord.Doubted)),
                "Six who have heard make it the house's: a breach's whole weight.");
            Knowledge.AddKnower(s, first, npcs[0].id);
            Assert.That(YourWord.Cost(s), Is.EqualTo(PlayerDeals.BrokenDealPenalty), "and no more for a seventh.");

            // A private fact never counts, nor somebody else's broken word.
            var hushed = BreakInFrontOfTheHouse(s, "deal-hushed", npcs[3].id);
            hushed.visibility = FactVisibility.Private;
            var theirs = new DealState { id = "deal-theirs", type = DealKind.SafetyAgreement, proposerId = npcs[4].id, recipientId = npcs[5].id,
                status = DealStatus.Broken, week = s.week, expiresWeek = s.week, trustImpact = DealKind.DefaultTrust(DealKind.SafetyAgreement),
                brokenById = npcs[4].id, settledWeek = s.week };
            s.deals.Add(theirs);
            Knowledge.BrokenWord(s, theirs, npcs[4].id, npcs[5].id);
            Assert.That(YourWord.Breaches(s).Select(f => f.id), Is.EqualTo(new[] { first.id }));
            Assert.That(YourWord.Cost(s), Is.EqualTo(PlayerDeals.BrokenDealPenalty));

            // Bounded: three breaches' worth at most.
            for (int i = 0; i < 3; i++)
            {
                var more = BreakInFrontOfTheHouse(s, "deal-more-" + i, npcs[i].id);
                foreach (var other in npcs) Knowledge.AddKnower(s, more, other.id);
            }
            Assert.That((YourWord.Cost(s), YourWord.Word(s)), Is.EqualTo((YourWord.Most, YourWord.Broken)));

            // Without the house's knowledge there is no reading, and C0's count stands.
            var plain = s.Clone();
            plain.story.rulesStartWeek = 0;
            Assert.That(YourWord.On(plain), Is.False);
            Assert.That((YourWord.Breaches(plain).Count, YourWord.Cost(plain)), Is.EqualTo((0, 0.0)));
            Assert.That(PlayerDeals.WordPenalty(plain), Is.EqualTo(PlayerDeals.BrokenDealPenalty * NpcDeals.BrokenDeals(plain, plain.playerId)));
        }

        [Test]
        public void TheOddsMoveWithTheReadingAndTheOddsThePlayerSeesTakeTheSameTerm()
        {
            var s = Season(41, 8);
            WithTheHousesKnowledge(s);
            var npcs = Npcs(s);
            var asked = npcs[6];
            // Clear of both clamps whatever the reading costs: a houseguest on nothing either way, no traits.
            asked.traits = new List<string>();
            SetScore(s, asked.id, s.playerId, 0);
            SetScore(s, s.playerId, asked.id, 0);
            var roll = DealKind.All.ToDictionary(kind => kind, kind => PlayerDeals.AcceptanceChance(s, asked.id, kind, null));
            var shown = DealKind.All.ToDictionary(kind => kind, kind => KnownOdds.Deal(s, asked.id, kind, null).chance);
            const string heardOf = "I've heard you've broken deals before. I can't trust that.";
            Assert.That(PlayerDeals.Reasoning(s, asked.id, DealKind.Partnership, false), Is.Not.EqualTo(heardOf));

            // Broken in front of the house, and heard of by two more: three hearings, a breach's half.
            var fact = BreakInFrontOfTheHouse(s, "deal-heard", npcs[0].id);
            Knowledge.AddKnower(s, fact, npcs[1].id);
            Knowledge.AddKnower(s, fact, npcs[2].id);
            const double cost = 3 * YourWord.PerHearing;
            bool Unclamped(double before) => before - cost > PlayerDeals.MinimumChance && before < PlayerDeals.MaximumChance;
            int checkedKinds = 0;
            foreach (string kind in DealKind.All)
            {
                if (Unclamped(roll[kind]))
                {
                    Assert.That(PlayerDeals.AcceptanceChance(s, asked.id, kind, null), Is.EqualTo(roll[kind] - cost).Within(1e-9),
                        kind + ": the roll takes the reading,");
                    checkedKinds++;
                }
                if (Unclamped(shown[kind]))
                    Assert.That(KnownOdds.Deal(s, asked.id, kind, null).chance, Is.EqualTo(shown[kind] - cost).Within(1e-9),
                        kind + ": and the odds the player sees take the same term.");
            }
            Assert.That(checkedKinds, Is.GreaterThan(5), "Most kinds of deal are clear of the clamps.");
            Assert.That(YourWord.Cost(s), Is.EqualTo(cost), "That is the reading's cost, which the player is shown.");
            Assert.That(PlayerDeals.Reasoning(s, asked.id, DealKind.Partnership, false), Is.Not.EqualTo(heardOf), "Six points is not the house talking.");

            // Two more broken by the player's ballots: on their record, and nothing the house heard - the
            // houseguest says nothing of hearing it, and the chance does not move.
            s.deals.Add(BrokenByThePlayer(s, "deal-ballot-1", DealKind.Partnership, npcs[4].id));
            s.deals.Add(BrokenByThePlayer(s, "deal-ballot-2", DealKind.Partnership, npcs[5].id));
            Assert.That(NpcDeals.BrokenDeals(s, s.playerId), Is.EqualTo(3), "Three broken by the player, on the record,");
            Assert.That(PlayerDeals.Reasoning(s, asked.id, DealKind.Partnership, false), Is.Not.EqualTo(heardOf), "and only the house's own hearing is 'heard'.");
            Assert.That(KnownOdds.Deal(s, asked.id, DealKind.Partnership, null).chance, Is.EqualTo(shown[DealKind.Partnership] - cost).Within(1e-9));

            // Heard of by the house twice over - by everybody but the one asked: past twenty points, and still
            // only somebody who has heard of a breach of the player's says they heard.
            var again = BreakInFrontOfTheHouse(s, "deal-again", npcs[3].id);
            foreach (var other in npcs.Where(n => n.id != asked.id)) { Knowledge.AddKnower(s, fact, other.id); Knowledge.AddKnower(s, again, other.id); }
            Assert.That(YourWord.Cost(s), Is.EqualTo(2 * PlayerDeals.BrokenDealPenalty));
            Assert.That(PlayerDeals.Reasoning(s, asked.id, DealKind.Partnership, false), Is.Not.EqualTo(heardOf),
                "A houseguest who knows of no breach of the player's does not say they heard of one.");
            Knowledge.AddKnower(s, again, asked.id);
            Assert.That(YourWord.Cost(s), Is.EqualTo(2 * PlayerDeals.BrokenDealPenalty), "No more for a seventh,");
            Assert.That(PlayerDeals.Reasoning(s, asked.id, DealKind.Partnership, false), Is.EqualTo(heardOf), "and now they have heard.");
            Assert.That(KnownOdds.Deal(s, asked.id, DealKind.Partnership, null).chance,
                Is.EqualTo(shown[DealKind.Partnership] - 2 * PlayerDeals.BrokenDealPenalty).Within(1e-9));
            Assert.That(YourWord.OddsLine(s), Is.EqualTo("Your word is broken: 7 houseguests have heard of you going back on it, "
                + "and it weighs on every deal and alliance you propose, but not on a plea or a price for the veto."));
        }

        [Test]
        public void ThePlayerIsToldWhatTheReadingWeighsOnAndNotEveryDeal()
        {
            var s = Season(41, 8);
            WithTheHousesKnowledge(s);
            var npcs = Npcs(s);
            var asked = npcs[6];
            // Clear of the clamps, as above: a houseguest on nothing either way, no traits, and nothing
            // between them and the player.
            asked.traits = new List<string>();
            SetScore(s, asked.id, s.playerId, 0);
            SetScore(s, s.playerId, asked.id, 0);
            // What it weighs on: a deal proposed at the table, and an alliance asked into - 'Propose an
            // alliance' and a bring-in both roll the alliance invitation's odds.
            double deal = PlayerDeals.AcceptanceChance(s, asked.id, DealKind.SafetyAgreement, null);
            double dealShown = KnownOdds.Deal(s, asked.id, DealKind.SafetyAgreement, null).chance;
            double alliance = EpisodeEngine.AllianceChance(s, asked.id);
            double allianceShown = KnownOdds.Alliance(s, asked.id).chance;
            // What it does not, though each strikes a deal the Your word page can list as one the player
            // proposed: the plea's 'Make a deal' and a veto for a price.
            double plea = StrategyRules.Chance(s, asked.id, LobbyAsk.Save, s.playerId, LobbyApproach.Deal);
            double pleaShown = KnownOdds.Plea(s, asked.id, LobbyAsk.Save, s.playerId, LobbyApproach.Deal).chance;
            double price = Negotiation.Chance(s, asked.id, Negotiation.VetoForAPrice, false);
            double priceShown = Negotiation.Chance(s, asked.id, Negotiation.VetoForAPrice, true);
            // Nor a story's choice, though a beat a conversation raised is answered in it and the deal it
            // strikes is the player's too: The Count's 'Make the pitch', on the story's own odds.
            var count = new HouseEventState { id = "house-event-the-count", kind = HouseEventKind.Story, surface = StorySurfaces.Conversation,
                title = "The Count", involvedIds = new List<string> { asked.id } };
            var pitch = new HouseEventChoice { optionId = "pitch", label = "Make the pitch", checkBase = 50, subjectId = asked.id,
                approach = Personality.Approach.Calculated };
            int story = StoryOdds.Chance(s, count, pitch);

            // Broken in front of the house and heard of by two more, none of them the one asked.
            var fact = BreakInFrontOfTheHouse(s, "deal-named", npcs[0].id);
            Knowledge.AddKnower(s, fact, npcs[1].id);
            Knowledge.AddKnower(s, fact, npcs[2].id);
            const double cost = 3 * YourWord.PerHearing;
            Assert.That(YourWord.Cost(s), Is.EqualTo(cost), "Precondition: three hearings.");
            foreach (double before in new[] { deal, dealShown, alliance, allianceShown })
                Assert.That(before - cost > PlayerDeals.MinimumChance && before < PlayerDeals.MaximumChance, Is.True, "Precondition: clear of the clamps.");
            Assert.That(story - cost > StoryOdds.Floor && story < StoryOdds.Ceiling, Is.True, "Precondition: the story's odds are clear of their clamps too.");
            Assert.That(PlayerDeals.AcceptanceChance(s, asked.id, DealKind.SafetyAgreement, null), Is.EqualTo(deal - cost).Within(1e-9),
                "A deal proposed at the table takes the reading,");
            Assert.That(KnownOdds.Deal(s, asked.id, DealKind.SafetyAgreement, null).chance, Is.EqualTo(dealShown - cost).Within(1e-9), "as shown,");
            Assert.That(EpisodeEngine.AllianceChance(s, asked.id), Is.EqualTo(alliance - cost).Within(1e-9), "and so does an alliance asked into,");
            Assert.That(KnownOdds.Alliance(s, asked.id).chance, Is.EqualTo(allianceShown - cost).Within(1e-9), "as shown;");
            Assert.That(StrategyRules.Chance(s, asked.id, LobbyAsk.Save, s.playerId, LobbyApproach.Deal), Is.EqualTo(plea), "the plea's deal does not,");
            Assert.That(KnownOdds.Plea(s, asked.id, LobbyAsk.Save, s.playerId, LobbyApproach.Deal).chance, Is.EqualTo(pleaShown), "as shown,");
            Assert.That(Negotiation.Chance(s, asked.id, Negotiation.VetoForAPrice, false), Is.EqualTo(price), "nor a veto for a price,");
            Assert.That(Negotiation.Chance(s, asked.id, Negotiation.VetoForAPrice, true), Is.EqualTo(priceShown), "as shown,");
            Assert.That(StoryOdds.Chance(s, count, pitch), Is.EqualTo(story), "nor a story's choice, its odds shown and rolled alike.");

            // So the words name what it weighs on, and what it does not by name. Over the odds, the plea and
            // the veto's price, which can be the first table under the note; on the Your word page, which
            // lists the deals all three strike as the player's, a story's choice as well.
            Assert.That(YourWord.OddsLine(s), Is.EqualTo("Your word is questioned: 3 houseguests have heard of you going back on it, "
                + "and it weighs on every deal and alliance you propose, but not on a plea or a price for the veto."));
            string heard = FinalistRead.FirstName(npcs[0].name) + ", " + FinalistRead.FirstName(npcs[1].name) + " and " + FinalistRead.FirstName(npcs[2].name);
            Assert.That(YourWord.Summary(s), Is.EqualTo("3 houseguests have heard of you going back on your word: " + heard
                + ". The further it spreads, the more it weighs on every deal and alliance you propose, "
                + "but not on a plea, a price for the veto or a story's choice."));
        }

        [Test]
        public void UnderTheRulesATrackRecordIsTheBreachesTheHouseHeardOf()
        {
            const string trackRecord = "Your track record concerns me.", unsure = "I'm not sure this is the right move for me.";
            foreach (bool story in new[] { false, true })
            {
                var s = Season(53, 8);
                if (story) EpisodeEngine.EnableStory(s);
                EpisodeEngine.EnableCommitments(s);
                var npcs = Npcs(s);
                string asked = npcs[6].id;
                // Warm enough by the player's own reading that the line goes past "I don't think I can trust you".
                SetScore(s, s.playerId, asked, 30);
                SetScore(s, asked, s.playerId, 30);
                Assert.That(PlayerDeals.Reasoning(s, asked, DealKind.Partnership, false), Is.EqualTo(unsure), "Nothing broken, nothing to say of it.");
                s.deals.Add(BrokenByThePlayer(s, "deal-by-ballot", DealKind.Partnership, npcs[0].id));
                if (story)
                {
                    Assert.That(PlayerDeals.Reasoning(s, asked, DealKind.Partnership, false), Is.EqualTo(unsure),
                        "A breach the house never heard of is no track record in its words.");
                    BreakInFrontOfTheHouse(s, "deal-seen", npcs[1].id);
                    Assert.That(PlayerDeals.Reasoning(s, asked, DealKind.Partnership, false), Is.EqualTo(trackRecord), "One it heard of is.");
                }
                else
                    Assert.That(PlayerDeals.Reasoning(s, asked, DealKind.Partnership, false), Is.EqualTo(trackRecord),
                        "Without the house's knowledge the record's own breaches are the track record, as C4 has it.");
            }
        }

        // ------------------------------------------------------------ X14

        [Test]
        public void X14_ThePlayersBrokenWordGoesOnlyWhenNothingElseIsLeft()
        {
            var s = Season(59, 8);
            EpisodeEngine.EnableStory(s);
            EpisodeEngine.EnableCommitments(s);
            var npcs = Npcs(s);
            var word = BreakInFrontOfTheHouse(s, "deal-oldest", npcs[0].id);
            // Every other fact an alliance's, and every one of those pacts still standing.
            for (int i = 1; i < Knowledge.Ceiling; i++)
            {
                s.alliances.Add(new AllianceState { id = "pact-" + i, name = "The Pact " + i, members = new List<string> { npcs[1].id, npcs[2].id }, active = true });
                s.story.facts.Add(new HouseFactState
                {
                    id = "fact-pact-" + i, kind = FactKinds.Alliance, refId = "pact-" + i, visibility = FactVisibility.Private, week = s.week,
                    knowers = new List<string> { npcs[1].id, npcs[2].id },
                });
            }
            Assert.That(s.story.facts[0], Is.SameAs(word), "Precondition: the player's broken word is the oldest fact.");
            var added = Knowledge.Create(s, FactKinds.Plan, npcs[4].id, npcs[5].id, FactVisibility.Private, null);
            Assert.That(s.story.facts, Has.Count.EqualTo(Knowledge.Ceiling));
            Assert.That(s.story.facts.Contains(added), Is.True);
            Assert.That(s.story.facts.Contains(word), Is.True, "The player's broken word, the oldest, is kept,");
            Assert.That(s.story.facts.Any(f => f.id == "fact-pact-1"), Is.False, "and the oldest alliance fact goes, though its pact stands.");
        }

        [Test]
        public void X14_UnderTheRulesAFullListNeverDropsAnAllianceFactSoAPrivatePactStaysPrivate()
        {
            foreach (bool rules in new[] { false, true })
            {
                var s = Season(43, 8);
                EpisodeEngine.EnableStory(s);
                if (rules) EpisodeEngine.EnableCommitments(s);
                var npcs = Npcs(s);
                var pact = new AllianceState { id = "alliance-secret", name = "The Secret", members = new List<string> { npcs[0].id, npcs[1].id }, active = true };
                s.alliances.Add(pact);
                Knowledge.AllianceFormed(s, pact);
                string outsider = npcs[2].id;
                Assert.That(Knowledge.AllianceVisibleTo(s, pact, outsider), Is.False, "Precondition: a pact nobody has told the outsider of.");
                var word = rules ? BreakInFrontOfTheHouse(s, "deal-kept-in-mind", npcs[3].id) : null;
                while (s.story.facts.Count < Knowledge.Ceiling)
                    Knowledge.Create(s, FactKinds.Plan, npcs[4].id, npcs[5].id, FactVisibility.Private, null);
                string oldestOther = s.story.facts.First(f => f.kind == FactKinds.Plan).id;
                Knowledge.Create(s, FactKinds.Plan, npcs[4].id, npcs[5].id, FactVisibility.Private, null);
                Assert.That(s.story.facts, Has.Count.EqualTo(Knowledge.Ceiling), "The list stays at its bound.");
                Valid(s);
                if (rules)
                {
                    Assert.That(Knowledge.Of(s, FactKinds.Alliance, pact.id), Is.Not.Null, "The pact's fact is kept,");
                    Assert.That(s.story.facts.Contains(word), Is.True, "and so is the player's broken word;");
                    Assert.That(s.story.facts.Any(f => f.id == oldestOther), Is.False, "the oldest fact that is neither goes,");
                    Assert.That(Knowledge.AllianceVisibleTo(s, pact, outsider), Is.False, "and the pact stays private.");
                }
                else
                {
                    Assert.That(Knowledge.Of(s, FactKinds.Alliance, pact.id), Is.Null, "Without the rules the oldest went, the pact's fact with it,");
                    Assert.That(Knowledge.AllianceVisibleTo(s, pact, outsider), Is.True,
                        "and a pact nobody told them of counted as known to every voter (X14), as it still does in those seasons.");
                }
            }
        }

        [Test]
        public void X14_AListOfNothingButAlliancesDropsTheOldestOfAPactNoLongerStanding()
        {
            var s = Season(47, 8);
            EpisodeEngine.EnableStory(s);
            EpisodeEngine.EnableCommitments(s);
            var npcs = Npcs(s);
            s.alliances.Add(new AllianceState { id = "pact-0", name = "The Standing", members = new List<string> { npcs[0].id, npcs[1].id }, active = true });
            s.alliances.Add(new AllianceState { id = "pact-1", name = "The Ended", members = new List<string> { npcs[2].id, npcs[3].id }, active = false });
            for (int i = 0; i < Knowledge.Ceiling; i++)
                s.story.facts.Add(new HouseFactState
                {
                    id = "fact-pact-" + i, kind = FactKinds.Alliance, refId = "pact-" + i, visibility = FactVisibility.Private, week = s.week,
                    knowers = new List<string> { npcs[0].id, npcs[1].id },
                });
            var added = Knowledge.Create(s, FactKinds.Plan, npcs[4].id, npcs[5].id, FactVisibility.Private, null);
            Assert.That(s.story.facts, Has.Count.EqualTo(Knowledge.Ceiling));
            Assert.That(s.story.facts.Contains(added), Is.True);
            Assert.That(s.story.facts.Any(f => f.id == "fact-pact-0"), Is.True, "The oldest is a pact that stands: kept.");
            Assert.That(s.story.facts.Any(f => f.id == "fact-pact-1"), Is.False, "The oldest fact of a pact that no longer stands goes.");
        }

        // ------------------------------------------------------------ without the rules

        [Test]
        public void WithoutTheRulesOrTheHousesKnowledgeABreachIsHeardOfAsItAlwaysWas()
        {
            int silentlyHeard = 0;
            foreach (var (rules, story) in new[] { (false, true), (true, false), (false, false) })
                for (uint seed = 29; seed < 41; seed++)
                {
                    var after = VetoKeptFromAPartner(rules, story, out _, seed);
                    string where = (rules ? "the rules" : "no rules") + ", " + (story ? "the story" : "no story") + ", seed " + seed;
                    Assert.That(YourWord.On(after), Is.False, where);
                    Assert.That(after.story.facts.Any(f => f.kind == FactKinds.BrokenWord), Is.False, where + ": no fact is written,");
                    Assert.That(YourWord.Cost(after), Is.Zero, where);
                    Assert.That(PlayerDeals.WordPenalty(after), Is.EqualTo(PlayerDeals.BrokenDealPenalty),
                        where + ": every breach the record holds against the player counts at the table, as it did.");
                    Assert.That(after.events.Any(IsAHearingLine), Is.False, where);
                    silentlyHeard += HeardOfThePlayer(after).Count;
                }
            Assert.That(silentlyHeard, Is.GreaterThan(0), "and the house heard of it at once, in silence, as its spread always had it.");
        }

        [Test]
        public void UnderTheRulesThePlayerHearsOfNobodyElsesBreachInSilence()
        {
            int heardBefore = 0, houseHeard = 0, judged = 0;
            for (uint seed = 1; seed <= 30; seed++)
                foreach (bool story in new[] { false, true })
                {
                    var after = AHeadBreaksAPactWithAHouseguest(story, seed, out string head);
                    if (after == null) continue;
                    judged++;
                    var mine = Entries(after, after.playerId, head, YourWord.HeardType);
                    if (story) Assert.That(mine, Is.Empty, "seed " + seed + ": the player's view of a breaker never moves without a line.");
                    else heardBefore += mine.Count;
                    if (story) houseHeard += after.contestants.Where(c => !c.isPlayer).Sum(c => Entries(after, c.id, head, YourWord.HeardType).Count);
                }
            Assert.That(judged, Is.GreaterThan(0), "Heads of Household broke their pacts.");
            Assert.That(heardBefore, Is.GreaterThan(0), "Without the house's knowledge the player heard of some in silence.");
            Assert.That(houseHeard, Is.GreaterThan(0), "The house still hears of a houseguest's breach as it always did.");
        }

        // ------------------------------------------------------------ fixtures

        private static EpisodeState Season(uint seed, int size) =>
            SeasonBuilder.Create(new SeasonBuilder.Choice { HouseSize = size }, seed);

        private static List<ContestantState> Npcs(EpisodeState s) => s.Active.Where(c => !c.isPlayer).ToList();

        /// <summary>The commitment rules and the story's knowledge: the house keeps the player's word (C8).</summary>
        private static void WithTheHousesKnowledge(EpisodeState s)
        {
            EpisodeEngine.EnableStory(s);
            EpisodeEngine.EnableCommitments(s);
        }

        private static void SetScore(EpisodeState s, string from, string to, double score)
        {
            var edge = s.relationships.FirstOrDefault(r => r.fromId == from && r.toId == to);
            if (edge == null) s.relationships.Add(edge = new RelationshipState { fromId = from, toId = to });
            edge.score = score;
        }

        private static void Valid(EpisodeState s) => Assert.That(EpisodeValidation.TryValidate(s, out var error), Is.True, error);

        private static EpisodeCommand Command(EpisodeState s, EpisodeCommandKind kind) =>
            new EpisodeCommand
            {
                id = "your-word-" + kind + "-" + s.revision, actorId = s.playerId, kind = kind,
                expectedRevision = s.revision, expectedPhase = s.phase,
            };

        /// <summary>A deal of the player's with somebody, broken by the player and settled this week, as the settlement records one under the rules.</summary>
        private static DealState BrokenByThePlayer(EpisodeState s, string id, string type, string with) =>
            new DealState
            {
                id = id, type = type, proposerId = s.playerId, recipientId = with, status = DealStatus.Broken, week = s.week,
                expiresWeek = s.week, trustImpact = DealKind.DefaultTrust(type), brokenById = s.playerId, settledWeek = s.week,
            };

        /// <summary>A safety deal the player broke in front of the house, on the record and as house knowledge the two of them hold.</summary>
        private static HouseFactState BreakInFrontOfTheHouse(EpisodeState s, string id, string with)
        {
            var deal = BrokenByThePlayer(s, id, DealKind.SafetyAgreement, with);
            s.deals.Add(deal);
            return Knowledge.BrokenWord(s, deal, s.playerId, with);
        }

        private static EpisodeState Without(EpisodeState s, string id)
        {
            var copy = s.Clone();
            copy.deals.RemoveAll(d => d.id == id);
            return copy;
        }

        private static List<RelationshipEventState> Entries(EpisodeState s, string from, string to, string type) =>
            s.relationships.Where(r => r.fromId == from && r.toId == to).SelectMany(r => r.events).Where(e => e.type == type).ToList();

        /// <summary>The entries of a type one houseguest wrote of another in the step from one state to the next.</summary>
        private static List<RelationshipEventState> NewEntries(EpisodeState before, EpisodeState after, string from, string to, string type) =>
            Entries(after, from, to, type).Where(e => e.sequence >= before.nextSequence).ToList();

        /// <summary>Every hearing of a betrayal the house holds against the player.</summary>
        private static List<RelationshipEventState> HeardOfThePlayer(EpisodeState s) =>
            s.relationships.Where(r => r.toId == s.playerId).SelectMany(r => r.events).Where(e => e.type == YourWord.HeardType).ToList();

        private static bool IsAHearingLine(EpisodeEvent e) =>
            e.text != null && e.text.StartsWith(YourWord.HeardOpening, System.StringComparison.Ordinal) && e.text.Contains(" heard you went back on your ");

        /// <summary>The veto's six: the Head of Household, the block and the holder, then the rest of the house in order.</summary>
        private static List<string> Lineup(EpisodeState s) =>
            new List<string> { s.hohId }.Concat(s.nominees).Concat(new[] { s.vetoHolderId }).Concat(s.Active.Select(c => c.id))
                .Where(id => !string.IsNullOrEmpty(id)).Distinct().Take(EpisodeEngine.VetoPlayerCount(s.Active.Count())).ToList();

        /// <summary>
        /// The veto meeting with the player holding the veto and their word given on it to a nominee, who
        /// keep the block: the deal is broken by the player, in front of the house.
        /// </summary>
        private static EpisodeState VetoKeptFromAPartner(bool rules, bool story, out string partner, uint seed = 29)
        {
            var s = Season(seed, 8);
            var npcs = Npcs(s);
            s.phase = EpisodePhase.VetoMeeting;
            s.hohId = npcs[0].id;
            s.nominees = new List<string> { npcs[1].id, npcs[2].id };
            foreach (var nominee in s.nominees) { s.Find(nominee).nominationWeeks.Add(s.week); s.Find(nominee).timesNominated = 1; }
            s.vetoHolderId = s.playerId;
            s.vetoPlayers = Lineup(s);
            partner = npcs[1].id;
            s.deals.Add(new DealState
            {
                id = VetoDealId, type = DealKind.VetoUse, proposerId = s.playerId, recipientId = partner, status = DealStatus.Active,
                week = s.week, expiresWeek = s.week, trustImpact = DealKind.DefaultTrust(DealKind.VetoUse),
            });
            if (story) EpisodeEngine.EnableStory(s);
            if (rules) EpisodeEngine.EnableCommitments(s);
            Valid(s);
            var command = Command(s, EpisodeCommandKind.ResolveVeto);
            command.useVeto = false;
            var kept = new EpisodeEngine(s).Apply(command);
            Assert.That(kept.accepted, Is.True, kept.reason);
            return kept.state;
        }

        /// <summary>
        /// A campaign with the player voting, their partnership with a nominee standing: their ballot votes
        /// the partner out, which breaks it, under the commitment rules - with the story's knowledge when
        /// <paramref name="story"/>.
        /// </summary>
        private static EpisodeState PartnerVotedOut(bool story, out string partner, out string bystander)
        {
            var s = ContentCatalog.Create(7);
            s.strategyRulesStartWeek = 1;
            var npcs = s.Active.Where(c => !c.isPlayer).Select(c => c.id).ToList();
            s.phase = EpisodePhase.Campaign;
            s.hohId = npcs[0];
            s.nominees = new List<string> { npcs[1], npcs[2] };
            s.vetoHolderId = npcs[3];
            s.vetoPlayers = Lineup(s);
            s.vetoResolved = true;
            partner = npcs[1];
            bystander = npcs[npcs.Count - 1];
            s.deals.Add(new DealState
            {
                id = "deal-partners", type = DealKind.Partnership, proposerId = s.playerId, recipientId = partner, status = DealStatus.Active,
                week = s.week, expiresWeek = 0, trustImpact = DealKind.DefaultTrust(DealKind.Partnership),
            });
            if (story) EpisodeEngine.EnableStory(s);
            EpisodeEngine.EnableCommitments(s);
            Valid(s);
            var engine = new EpisodeEngine(s);
            // The plain answers: the player's ballot is for the first nominee - the partner.
            for (int i = 0; i < 40 && !engine.Snapshot.evictionResolved; i++)
            {
                var result = engine.Apply(EpisodeEngineTests.NextCommand(engine.Snapshot));
                Assert.That(result.accepted, Is.True, result.reason);
            }
            Assert.That(engine.Snapshot.votes.Single(v => v.voterId == s.playerId).targetId, Is.EqualTo(partner), "Precondition: the player voted their partner out.");
            return engine.Snapshot;
        }

        /// <summary>
        /// A houseguest Head of Household with a safety pact with another, who puts them up: the pact is broken
        /// by the head, between two houseguests - under the commitment rules, with the story's knowledge when
        /// <paramref name="story"/>. Null when the nominations went elsewhere.
        /// </summary>
        private static EpisodeState AHeadBreaksAPactWithAHouseguest(bool story, uint seed, out string head)
        {
            var s = Season(seed, 8);
            var npcs = Npcs(s);
            string hoh = npcs[0].id, partner = npcs[1].id;
            head = hoh;
            s.phase = EpisodePhase.Nomination;
            s.hohId = hoh;
            foreach (var other in s.contestants.Where(c => c.id != hoh))
                SetScore(s, hoh, other.id, other.id == partner ? -90 : other.isPlayer ? 90 : 60);
            s.deals.Add(new DealState
            {
                id = "deal-houseguests", type = DealKind.SafetyAgreement, proposerId = partner, recipientId = hoh, status = DealStatus.Active,
                week = s.week, expiresWeek = s.week + 1, trustImpact = DealKind.DefaultTrust(DealKind.SafetyAgreement),
            });
            if (story) EpisodeEngine.EnableStory(s);
            EpisodeEngine.EnableCommitments(s);
            Valid(s);
            var engine = new EpisodeEngine(s);
            var named = engine.Apply(EpisodeEngineTests.NextCommand(engine.Snapshot));
            Assert.That(named.accepted, Is.True, named.reason);
            var deal = named.state.deals.Single(d => d.id == "deal-houseguests");
            return deal.status == DealStatus.Broken && deal.brokenById == hoh ? named.state : null;
        }
    }
}
