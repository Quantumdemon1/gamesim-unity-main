using System.Collections.Generic;
using System.Linq;
using Gamesim.Simulation;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// The odds the player is shown come from what the player knows (ACTIONS-DEALS-ALLIANCES-PLAN
    /// V6, X9's odds half): how a houseguest privately sees anybody, and a pact the player has not
    /// heard of, never move the word; a pact the player knows of does; and the read says when it has
    /// little to go on. The roll's own numbers are pinned where they always were (PlayerDealTests,
    /// StrategyRulesTests). Unity-free, so the dotnet subset runs it (Tools/SimulationTests).
    /// </summary>
    public sealed class KnownOddsTests
    {
        private static readonly string[] Approaches = LobbyApproach.All;

        // ---------------------------------------------------------------- what the player cannot see

        [Test]
        public void TwoHouseguestsWhoSeeYouDifferentlyButWhomYouKnowAlikeShowTheSameWord()
        {
            var s = Season(21);
            var a = Plain(Npcs(s)[0]); var b = Plain(Npcs(s)[1]);
            Set(s, s.playerId, a.id, 30); Set(s, s.playerId, b.id, 30);
            Set(s, a.id, s.playerId, 90); Set(s, b.id, s.playerId, -90);

            foreach (string type in new[] { DealKind.Partnership, DealKind.SafetyAgreement, DealKind.AllianceInvite, DealKind.InformationSharing })
            {
                Assert.That(PlayerDeals.CanPropose(s, a.id, type, null, out string why), Is.True, type + ": " + why);
                Assert.That(KnownOdds.Word(PlayerDeals.AcceptanceChance(s, a.id, type, null)),
                    Is.Not.EqualTo(KnownOdds.Word(PlayerDeals.AcceptanceChance(s, b.id, type, null))),
                    type + ": the roll reads how each of them really sees you, and they see you very differently.");
                var warm = KnownOdds.Deal(s, a.id, type, null);
                var cold = KnownOdds.Deal(s, b.id, type, null);
                Assert.That(cold.chance, Is.EqualTo(warm.chance).Within(1e-9), type);
                Assert.That(cold.word, Is.EqualTo(warm.word), type + ": you know them alike, so you are shown the same.");
            }

            foreach (string approach in Approaches)
            {
                Assert.That(KnownOdds.Word(StrategyRules.Chance(s, a.id, LobbyAsk.Spare, s.playerId, approach)),
                    Is.Not.EqualTo(KnownOdds.Word(StrategyRules.Chance(s, b.id, LobbyAsk.Spare, s.playerId, approach))), approach);
                Assert.That(KnownOdds.Plea(s, b.id, LobbyAsk.Spare, s.playerId, approach).word,
                    Is.EqualTo(KnownOdds.Plea(s, a.id, LobbyAsk.Spare, s.playerId, approach).word), approach + ": a plea, the same.");
            }
        }

        [Test]
        public void TwoPeopleAnAskIsAboutShowTheSameWordWhileYouKnowNothingOfWhereTheyStand()
        {
            var s = AtNomination(Season(22));
            var hoh = Plain(s.Find(s.hohId));
            var x = Npcs(s)[1]; var y = Npcs(s)[2];
            Set(s, hoh.id, x.id, 80); Set(s, hoh.id, y.id, -80);
            Fact(s, Pact(s, "secret", hoh.id, x.id), hoh.id, x.id);

            Assert.That(KnownOdds.Word(PlayerDeals.AcceptanceChance(s, hoh.id, DealKind.TargetAgreement, x.id)), Is.EqualTo(KnownOdds.Unlikely),
                "The roll sees a friend and a secret ally.");
            Assert.That(KnownOdds.Word(PlayerDeals.AcceptanceChance(s, hoh.id, DealKind.TargetAgreement, y.id)), Is.EqualTo(KnownOdds.Favourable),
                "and somebody they cannot stand.");
            var aboutX = KnownOdds.Deal(s, hoh.id, DealKind.TargetAgreement, x.id);
            var aboutY = KnownOdds.Deal(s, hoh.id, DealKind.TargetAgreement, y.id);
            Assert.That(aboutX.chance, Is.EqualTo(aboutY.chance).Within(1e-9));
            Assert.That(aboutX.word, Is.EqualTo(aboutY.word), "Lining the rows up must not say whom they like or who they are secretly with.");

            foreach (string approach in Approaches)
            {
                Assert.That(KnownOdds.Word(StrategyRules.Chance(s, hoh.id, LobbyAsk.Target, x.id, approach)),
                    Is.Not.EqualTo(KnownOdds.Word(StrategyRules.Chance(s, hoh.id, LobbyAsk.Target, y.id, approach))), approach);
                Assert.That(KnownOdds.Plea(s, hoh.id, LobbyAsk.Target, x.id, approach).word,
                    Is.EqualTo(KnownOdds.Plea(s, hoh.id, LobbyAsk.Target, y.id, approach).word), approach + ": nor the pleas.");
            }
        }

        [Test]
        public void APactYouDoNotKnowOfNeverMovesTheWord()
        {
            var s = AtMeeting(Season(24));
            var hoh = Plain(s.Find(s.hohId));
            var holder = Plain(s.Find(s.vetoHolderId));
            string first = s.nominees[0], free = Npcs(s)[4].id;
            Set(s, s.playerId, holder.id, 20); Set(s, s.playerId, hoh.id, 20);

            string Shown() => string.Join(" | ", Asks(s, hoh.id, holder.id, first, free).Select(e => e.word + " " + e.chance.ToString("0.###")));
            double Rolled() => RolledChances(s, hoh.id, holder.id, first, free).Sum();
            string shown = Shown();
            double rolled = Rolled();

            Pact(s, "no-fact", holder.id, first);
            Fact(s, Pact(s, "members-only", hoh.id, free), hoh.id, free);
            Assert.That(Rolled(), Is.Not.EqualTo(rolled).Within(1e-9), "The roll reads both pacts.");
            Assert.That(Shown(), Is.EqualTo(shown), "Neither has reached the player, so nothing on screen moves.");
        }

        [Test]
        public void APactYouKnowOfChangesTheWord()
        {
            var s = AtNomination(Season(23));
            var hoh = Plain(s.Find(s.hohId));
            var x = Npcs(s)[1];
            Set(s, s.playerId, hoh.id, 40);

            var before = KnownOdds.Deal(s, hoh.id, DealKind.TargetAgreement, x.id);
            var plea = KnownOdds.Plea(s, hoh.id, LobbyAsk.Target, x.id, LobbyApproach.Strategic);
            Assert.That(before.word, Is.EqualTo(KnownOdds.Favourable));
            Assert.That(plea.chance, Is.EqualTo(58), "Fifty, and a fifth of your reading of them.");

            var fact = Fact(s, Pact(s, "pact", hoh.id, x.id), hoh.id, x.id);
            Assert.That(KnownOdds.Deal(s, hoh.id, DealKind.TargetAgreement, x.id).word, Is.EqualTo(before.word),
                "A pact only its members know of moves nothing on screen.");

            fact.knowers.Add(s.playerId);
            var after = KnownOdds.Deal(s, hoh.id, DealKind.TargetAgreement, x.id);
            Assert.That(after.chance, Is.EqualTo(before.chance - 40).Within(1e-9), "The roll's own term for their ally.");
            Assert.That(after.word, Is.EqualTo(KnownOdds.Unlikely));
            Assert.That(KnownOdds.Plea(s, hoh.id, LobbyAsk.Target, x.id, LobbyApproach.Strategic).word, Is.EqualTo(KnownOdds.Unlikely),
                "and asking the Head of Household to nominate their known ally is a long shot too.");

            fact.knowers.Remove(s.playerId);
            fact.visibility = FactVisibility.Public;
            Assert.That(KnownOdds.Deal(s, hoh.id, DealKind.TargetAgreement, x.id).chance, Is.EqualTo(after.chance).Within(1e-9),
                "A pact the whole house knows of is known to the player too.");
        }

        [Test]
        public void AVoterKnownToBeCloseToTheOtherNomineeIsAHarderAsk()
        {
            var s = Season(30);
            s.phase = EpisodePhase.Campaign;
            var other = Npcs(s)[1].id;
            s.nominees = new List<string> { s.playerId, other };
            var voter = Plain(Npcs(s)[2]);
            Assert.That(KnownOdds.Plea(s, voter.id, LobbyAsk.Vote, s.playerId, LobbyApproach.Strategic).chance, Is.EqualTo(60),
                "Fifty, and ten for pleading from the block.");

            Learn(s, voter.id, other, ClaimSource.Overheard, 40);
            Assert.That(KnownOdds.Plea(s, voter.id, LobbyAsk.Vote, s.playerId, LobbyApproach.Strategic).chance, Is.EqualTo(45),
                "You heard they are close to the other nominee.");

            Fact(s, Pact(s, "with-them", voter.id, other), voter.id, other, s.playerId);
            Assert.That(KnownOdds.Plea(s, voter.id, LobbyAsk.Vote, s.playerId, LobbyApproach.Strategic).chance, Is.EqualTo(40),
                "You know they are allied with them: the roll's ally term, in place of its warmth term.");
        }

        [Test]
        public void AKnownPactThatEndsOutOfSightStillReadsAsOne()
        {
            var s = AtNomination(Season(25));
            var hoh = Plain(s.Find(s.hohId));
            var x = Npcs(s)[1];
            var seen = Pact(s, "seen", hoh.id, x.id);
            Fact(s, seen, hoh.id, x.id, s.playerId);
            double shown = KnownOdds.Deal(s, hoh.id, DealKind.TargetAgreement, x.id).chance;
            double rolled = PlayerDeals.AcceptanceChance(s, hoh.id, DealKind.TargetAgreement, x.id);

            seen.active = false;
            Assert.That(PlayerDeals.AcceptanceChance(s, hoh.id, DealKind.TargetAgreement, x.id), Is.GreaterThan(rolled),
                "The roll knows the pact soured.");
            Assert.That(KnownOdds.Deal(s, hoh.id, DealKind.TargetAgreement, x.id).chance, Is.EqualTo(shown).Within(1e-9),
                "It soured on scores the player never sees, so the word does not say so either.");

            var ours = Pact(s, "ours", s.playerId, hoh.id);
            double allied = KnownOdds.Deal(s, hoh.id, DealKind.Partnership, null).chance;
            ours.active = false;
            Assert.That(KnownOdds.Deal(s, hoh.id, DealKind.Partnership, null).chance, Is.EqualTo(allied - 35).Within(1e-9),
                "The player is told when their own pact falls apart, so theirs counts only while it stands.");
        }

        // ---------------------------------------------------------------- what the player has

        [Test]
        public void ACurrentReadHoldsYourOwnReadingInsideItsBandAndNeverPassesOnItsNumber()
        {
            var s = Season(28);
            s.week = 4;
            var npcs = Npcs(s).Select(Plain).ToList();
            foreach (var npc in npcs) Set(s, s.playerId, npc.id, 0);
            Set(s, npcs[0].id, s.playerId, 30); Set(s, npcs[1].id, s.playerId, 95);
            Learn(s, npcs[0].id, s.playerId, ClaimSource.Read, 30);
            Learn(s, npcs[1].id, s.playerId, ClaimSource.Read, 95);

            Assert.That(KnownOdds.PresumedView(s, npcs[0].id), Is.EqualTo(KnownOdds.WarmLine), "Warm on you: at least the band's own line.");
            Assert.That(KnownOdds.PresumedView(s, npcs[1].id), Is.EqualTo(KnownOdds.WarmLine), "The read said warm, not ninety-five.");
            Assert.That(KnownOdds.Deal(s, npcs[1].id, DealKind.Partnership, null).word, Is.EqualTo(KnownOdds.Deal(s, npcs[0].id, DealKind.Partnership, null).word));
            Assert.That(KnownOdds.Deal(s, npcs[0].id, DealKind.Partnership, null).word, Is.EqualTo(KnownOdds.Favourable));
            Assert.That(KnownOdds.Deal(s, npcs[2].id, DealKind.Partnership, null).word, Is.EqualTo(KnownOdds.AboutEven),
                "Without a read, your own reading of them is all there is.");

            Set(s, s.playerId, npcs[0].id, 60);
            Assert.That(KnownOdds.PresumedView(s, npcs[0].id), Is.EqualTo(60), "Your own reading, where the read allows it.");

            Learn(s, npcs[2].id, s.playerId, ClaimSource.Read, -60);
            Set(s, s.playerId, npcs[2].id, 10);
            Assert.That(KnownOdds.PresumedView(s, npcs[2].id), Is.EqualTo(KnownOdds.ColdLine), "Cold on you, whatever you think of them.");

            Learn(s, npcs[3].id, s.playerId, ClaimSource.Read, 10);
            Set(s, s.playerId, npcs[3].id, 60);
            Assert.That(KnownOdds.PresumedView(s, npcs[3].id), Is.EqualTo(KnownOdds.WarmLine - 1), "Not sure about you.");
            Set(s, s.playerId, npcs[3].id, -60);
            Assert.That(KnownOdds.PresumedView(s, npcs[3].id), Is.EqualTo(KnownOdds.ColdLine + 1));

            Learn(s, npcs[4].id, s.playerId, ClaimSource.Missed, 0);
            Set(s, s.playerId, npcs[4].id, 12);
            Assert.That(KnownOdds.HasRead(s, npcs[4].id), Is.False, "A read that missed taught nothing.");
            Assert.That(KnownOdds.PresumedView(s, npcs[4].id), Is.EqualTo(12));

            Learn(s, npcs[5].id, s.playerId, ClaimSource.Read, 90, s.week - VoteRead.StandingShelfLife - 1);
            Set(s, s.playerId, npcs[5].id, -5);
            Assert.That(KnownOdds.HasRead(s, npcs[5].id), Is.False, "A read past the vote read's shelf life is not current.");
            Assert.That(KnownOdds.PresumedView(s, npcs[5].id), Is.EqualTo(-5));
        }

        [Test]
        public void TheReadSaysWhenItHasLittleToGoOn()
        {
            var s = Season(26);
            s.week = 6;
            var npc = Plain(Npcs(s)[0]);
            var x = Npcs(s)[1];
            var nothing = KnownOdds.Deal(s, npc.id, DealKind.Partnership, null);
            Assert.That((nothing.read, nothing.claim, nothing.history), Is.EqualTo((false, false, 0)));
            Assert.That(nothing.unknowns, Is.EqualTo(KnownOdds.Many), "No read, no claim, no history.");
            Assert.That(KnownOdds.Unknowns(s, npc.id), Is.EqualTo(KnownOdds.Many));

            s.deals.Add(new DealState
            {
                id = "deal-once", type = DealKind.Partnership, proposerId = s.playerId, recipientId = npc.id,
                status = DealStatus.Declined, week = 2, trustImpact = DealTrust.Medium,
            });
            Assert.That(KnownOdds.History(s, npc.id), Is.EqualTo(1));
            Assert.That(KnownOdds.Unknowns(s, npc.id), Is.EqualTo(KnownOdds.Many), "One record is not yet a history.");

            var read = Learn(s, npc.id, s.playerId, ClaimSource.Read, 40);
            Assert.That(KnownOdds.Unknowns(s, npc.id), Is.EqualTo(KnownOdds.Some), "A read is something to go on.");
            read.week = s.week - VoteRead.StandingShelfLife - 1;
            Assert.That(KnownOdds.Unknowns(s, npc.id), Is.EqualTo(KnownOdds.Many), "An old read is not.");
            read.week = s.week;

            s.promises.Add(new PromiseState
            {
                id = "promise-safety", fromId = s.playerId, toId = npc.id, kind = PromiseKind.Safety,
                status = PromiseStatus.Active, week = s.week,
            });
            Assert.That(KnownOdds.History(s, npc.id), Is.EqualTo(2));
            Assert.That(KnownOdds.Unknowns(s, npc.id), Is.EqualTo(KnownOdds.Few), "A current read and a history.");
            Assert.That(KnownOdds.Deal(s, npc.id, DealKind.Partnership, null).unknowns, Is.EqualTo(KnownOdds.Few));

            var aboutX = KnownOdds.Deal(s, npc.id, DealKind.TargetAgreement, x.id);
            Assert.That(aboutX.aboutKnown, Is.False);
            Assert.That(aboutX.unknowns, Is.EqualTo(KnownOdds.Some), "Nothing on where they stand with the person it is about.");
            Learn(s, npc.id, x.id, ClaimSource.Overheard, -40);
            var heard = KnownOdds.Deal(s, npc.id, DealKind.TargetAgreement, x.id);
            Assert.That(heard.unknowns, Is.EqualTo(KnownOdds.Few));
            Assert.That(heard.chance, Is.EqualTo(aboutX.chance + 20).Within(1e-9), "They sounded like they cannot stand them: the roll's own term.");

            var t = Season(27);
            var voter = Npcs(t)[0];
            t.ledger.claims.Add(new ClaimRow { week = t.week, voterId = voter.id, targetId = Npcs(t)[1].id, source = ClaimSource.Told });
            Assert.That(KnownOdds.HasClaim(t, voter.id), Is.True);
            Assert.That(KnownOdds.Unknowns(t, voter.id), Is.EqualTo(KnownOdds.Some), "What they said about the vote is something to go on.");
        }

        // ---------------------------------------------------------------- the words, and nothing else

        [Test]
        public void TheWordsAreTheDealTablesOwn()
        {
            var cases = new (double chance, string word)[]
            {
                (95, KnownOdds.Likely), (75, KnownOdds.Likely), (74.99, KnownOdds.Favourable), (55, KnownOdds.Favourable),
                (54.99, KnownOdds.AboutEven), (45, KnownOdds.AboutEven), (44.99, KnownOdds.AStretch), (25, KnownOdds.AStretch),
                (24.99, KnownOdds.Unlikely), (5, KnownOdds.Unlikely),
            };
            foreach (var (chance, word) in cases) Assert.That(KnownOdds.Word(chance), Is.EqualTo(word), chance.ToString());
        }

        [Test]
        public void ReadingTheOddsChangesNothingAndDrawsNothing()
        {
            var s = AtMeeting(Season(29));
            var hoh = s.Find(s.hohId); var holder = s.Find(s.vetoHolderId);
            string first = s.nominees[0], free = Npcs(s)[4].id;
            Fact(s, Pact(s, "pact", hoh.id, free), hoh.id, free, s.playerId);
            Learn(s, holder.id, s.playerId, ClaimSource.Read, 30);
            uint random = s.randomState; int sequence = s.nextSequence, revision = s.revision;
            int facts = s.story.facts.Count, standings = s.ledger.standings.Count;
            double scores = s.relationships.Sum(r => r.score);

            Assert.That(Asks(s, hoh.id, holder.id, first, free), Has.All.Matches<KnownOdds.Estimate>(e => e.chance >= 5 && e.chance <= 95));
            KnownOdds.Unknowns(s, holder.id);

            Assert.That((s.randomState, s.nextSequence, s.revision), Is.EqualTo((random, sequence, revision)));
            Assert.That((s.story.facts.Count, s.ledger.standings.Count), Is.EqualTo((facts, standings)));
            Assert.That(s.relationships.Sum(r => r.score), Is.EqualTo(scores));
        }

        // ---------------------------------------------------------------- fixtures

        /// <summary>Every ask this file lines up, as the screen would show them.</summary>
        private static List<KnownOdds.Estimate> Asks(EpisodeState s, string hoh, string holder, string nominee, string free)
        {
            var asks = new List<KnownOdds.Estimate>
            {
                KnownOdds.Deal(s, holder, DealKind.TargetAgreement, free),
                KnownOdds.Deal(s, holder, DealKind.TargetAgreement, nominee),
                KnownOdds.Deal(s, hoh, DealKind.TargetAgreement, free),
                KnownOdds.Deal(s, holder, DealKind.VoteSave, nominee),
                KnownOdds.Deal(s, holder, DealKind.VoteEvict, nominee),
            };
            foreach (string approach in Approaches)
            {
                asks.Add(KnownOdds.Plea(s, holder, LobbyAsk.Save, nominee, approach));
                asks.Add(KnownOdds.Plea(s, holder, LobbyAsk.Keep, null, approach));
                asks.Add(KnownOdds.Plea(s, hoh, LobbyAsk.Target, free, approach));
                asks.Add(KnownOdds.Plea(s, hoh, LobbyAsk.Spare, s.playerId, approach));
            }
            return asks;
        }

        /// <summary>The same asks, as the roll weighs them.</summary>
        private static List<double> RolledChances(EpisodeState s, string hoh, string holder, string nominee, string free)
        {
            var rolls = new List<double>
            {
                PlayerDeals.AcceptanceChance(s, holder, DealKind.TargetAgreement, free),
                PlayerDeals.AcceptanceChance(s, holder, DealKind.TargetAgreement, nominee),
                PlayerDeals.AcceptanceChance(s, hoh, DealKind.TargetAgreement, free),
                PlayerDeals.AcceptanceChance(s, holder, DealKind.VoteSave, nominee),
                PlayerDeals.AcceptanceChance(s, holder, DealKind.VoteEvict, nominee),
            };
            foreach (string approach in Approaches)
            {
                rolls.Add(StrategyRules.Chance(s, holder, LobbyAsk.Save, nominee, approach));
                rolls.Add(StrategyRules.Chance(s, holder, LobbyAsk.Keep, null, approach));
                rolls.Add(StrategyRules.Chance(s, hoh, LobbyAsk.Target, free, approach));
                rolls.Add(StrategyRules.Chance(s, hoh, LobbyAsk.Spare, s.playerId, approach));
            }
            return rolls;
        }

        private static EpisodeState Season(uint seed) =>
            SeasonBuilder.Create(new SeasonBuilder.Choice { HouseSize = 8 }, seed);

        private static List<ContestantState> Npcs(EpisodeState s) => s.Active.Where(c => !c.isPlayer).ToList();

        /// <summary>No traits: a houseguest's traits are on the conversation's header, and two the player knows alike must share them too.</summary>
        private static ContestantState Plain(ContestantState npc)
        {
            npc.traits = new List<string>();
            return npc;
        }

        private static void Set(EpisodeState s, string from, string to, double score)
        {
            var row = s.relationships.FirstOrDefault(r => r.fromId == from && r.toId == to);
            if (row == null) s.relationships.Add(row = new RelationshipState { fromId = from, toId = to });
            row.score = score;
        }

        private static AllianceState Pact(EpisodeState s, string id, params string[] members)
        {
            var pact = new AllianceState { id = id, name = "The " + id, members = members.ToList() };
            s.alliances.Add(pact);
            return pact;
        }

        /// <summary>A pact's private fact, and who has heard of it.</summary>
        private static HouseFactState Fact(EpisodeState s, AllianceState pact, params string[] knowers)
        {
            var fact = new HouseFactState
            {
                id = "fact-" + pact.id, kind = FactKinds.Alliance, refId = pact.id, visibility = FactVisibility.Private,
                actorId = pact.members[0], subjectId = pact.members[1], week = s.week, knowers = knowers.ToList(),
            };
            s.story.facts.Add(fact);
            return fact;
        }

        /// <summary>A standing the player learned this week, or in the week given.</summary>
        private static StandingRow Learn(EpisodeState s, string from, string to, string source, double score, int? week = null)
        {
            var row = new StandingRow { week = week ?? s.week, fromId = from, toId = to, source = source, score = score };
            s.ledger.standings.Add(row);
            return row;
        }

        /// <summary>Before nominations: the first houseguest is Head of Household and nobody is up yet.</summary>
        private static EpisodeState AtNomination(EpisodeState s)
        {
            s.phase = EpisodePhase.Nomination;
            s.hohId = Npcs(s)[0].id;
            s.nominees.Clear();
            return s;
        }

        /// <summary>The veto meeting: the first houseguest is Head of Household, the next two nominated, the fourth holding the veto.</summary>
        private static EpisodeState AtMeeting(EpisodeState s)
        {
            var npcs = Npcs(s).Select(c => c.id).ToList();
            s.phase = EpisodePhase.VetoMeeting;
            s.hohId = npcs[0];
            s.nominees = new List<string> { npcs[1], npcs[2] };
            s.vetoHolderId = npcs[3];
            s.vetoResolved = false;
            return s;
        }
    }
}
