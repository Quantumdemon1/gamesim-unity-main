using System.Collections.Generic;
using System.Linq;
using Gamesim.Simulation;
using Newtonsoft.Json;
using NUnit.Framework;
#if UNITY_5_3_OR_NEWER
using Gamesim.Presentation;
#endif

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// ENDGAME-PLAN F2's gate: what the finalist cards say about the other two is evidence the
    /// player holds - the public record, what they were party to, what they heard - each line with
    /// how sure it is, and never the jury model's own numbers. Unity-free, so the dotnet subset
    /// runs it (Tools/SimulationTests).
    /// </summary>
    public sealed class FinalistReadTests
    {
        /// <summary>A Final 3 at the final eviction: the player as Head of Household with two finalists, three jurors.</summary>
        private static EpisodeState FinalThree(uint seed = 31)
        {
            var s = SeasonBuilder.Create(new SeasonBuilder.Choice { HouseSize = 6 }, seed);
            s.week = 9;
            foreach (var juror in s.contestants.Where(c => !c.isPlayer).Take(3)) juror.status = ContestantStatus.Jury;
            s.phase = EpisodePhase.FinalEviction;
            s.hohId = s.playerId;
            return s;
        }

        private static ContestantState Finalist(EpisodeState s, int index) => FinalistRead.Others(s)[index];
        private static ContestantState Juror(EpisodeState s, int index) => FinalistRead.Jurors(s)[index];

        private static void SetScore(EpisodeState s, string from, string to, double score)
        {
            var row = s.relationships.FirstOrDefault(r => r.fromId == from && r.toId == to);
            if (row == null) s.relationships.Add(row = new RelationshipState { fromId = from, toId = to });
            row.score = score;
        }

        private static AllianceState Alliance(EpisodeState s, string id, bool active, params string[] members)
        {
            var alliance = new AllianceState { id = id, name = "The " + id, active = active, members = members.ToList() };
            s.alliances.Add(alliance);
            return alliance;
        }

        private static string AllText(FinalistRead.Finalist read, IEnumerable<string> bullets = null) =>
            string.Join("\n", read.Facts.Select(f => f.label + ": " + f.value + " (" + f.certainty + ")")
                .Concat(read.jurors.Select(j => j.name + " (" + j.reason + ")")).Concat(new[] { read.line, read.standingLine })
                .Concat(bullets ?? Enumerable.Empty<string>()));

        [Test]
        public void WithNothingLearnedEveryJurorIsUncertainAndNothingIsInvented()
        {
            var s = FinalThree();
            Assert.That(FinalistRead.Others(s), Has.Count.EqualTo(2));
            Assert.That(FinalistRead.Jurors(s), Has.Count.EqualTo(3));
            foreach (var finalist in FinalistRead.Others(s))
            {
                var read = FinalistRead.Read(s, finalist.id);
                Assert.That(read.jurors.All(j => j.lean == FinalistRead.Uncertain && j.certainty == FinalistRead.Unknown), Is.True);
                Assert.That(read.support.value, Is.EqualTo("None known"));
                Assert.That(read.support.certainty, Is.EqualTo(FinalistRead.Unknown));
                Assert.That(read.bitterness.value, Is.EqualTo("None known"));
                Assert.That(read.uncertain.value, Is.EqualTo("3 of 3"));
                Assert.That(read.alliances.value, Is.EqualTo("None known"));
                Assert.That(read.alliances.certainty, Is.EqualTo(FinalistRead.Unknown));
                Assert.That(read.agreement.value, Is.EqualTo("None with you"));
                Assert.That(read.agreement.certainty, Is.EqualTo(FinalistRead.Unknown), "No agreement: nothing to go on.");
                Assert.That(read.line, Is.EqualTo(WebIntroductions.IntroLine(unchecked((int)s.seed), finalist.id, finalist.name, finalist.traits)),
                    "Their own line: the one they introduced themselves with.");
            }
        }

        [Test]
        public void AFinalTwoDealIsConfirmedAndSaysWhatEachChoiceDoesToIt()
        {
            var s = FinalThree();
            var kept = Finalist(s, 0); var other = Finalist(s, 1);
            s.deals.Add(new DealState { id = "deal-f2", type = DealKind.FinalTwo, proposerId = s.playerId, recipientId = kept.id, status = DealStatus.Active, week = 7 });
            var read = FinalistRead.Read(s, kept.id);
            Assert.That(read.agreement.value, Is.EqualTo("Deal: agreed"));
            Assert.That(read.agreement.certainty, Is.EqualTo(FinalistRead.Confirmed));
            string keptFirst = kept.name.Split(' ')[0], otherFirst = other.name.Split(' ')[0];
            Assert.That(FinalistRead.IfYouTake(s, kept.id, other.id), Does.Contain("Honours your Final 2 deal with " + keptFirst + "."));
            Assert.That(FinalistRead.IfYouTake(s, other.id, kept.id), Does.Contain("Breaks your Final 2 deal with " + keptFirst + "."));
            Assert.That(FinalistRead.IfYouTake(s, other.id, kept.id), Does.Contain(keptFirst + " joins the jury."));
            Assert.That(FinalistRead.Read(s, other.id).agreement.value, Is.EqualTo("None with you"));
        }

        [Test]
        public void ADealBetweenTwoHouseguestsIsNotThePlayersToSee()
        {
            var s = FinalThree();
            var finalist = Finalist(s, 0); var juror = Juror(s, 0); var other = Finalist(s, 1);
            s.deals.Add(new DealState { id = "npc-f2", type = DealKind.FinalTwo, proposerId = finalist.id, recipientId = other.id, status = DealStatus.Active, week = 7 });
            s.promises.Add(new PromiseState { id = "npc-promise", fromId = juror.id, toId = finalist.id, kind = PromiseKind.FinalTwo, status = PromiseStatus.Active, week = 6 });
            var read = FinalistRead.Read(s, finalist.id);
            Assert.That(read.agreement.value, Is.EqualTo("None with you"));
            Assert.That(read.jurors.Single(j => j.jurorId == juror.id).lean, Is.EqualTo(FinalistRead.Uncertain));
            Assert.That(FinalistRead.IfYouTake(s, finalist.id, other.id).Any(line => line.Contains("deal")), Is.False);
        }

        [Test]
        public void AnAllianceCountsOnlyAsFarAsThePlayerKnowsOfIt()
        {
            var s = FinalThree();
            var finalist = Finalist(s, 0); var juror = Juror(s, 0);
            var pact = Alliance(s, "pact", true, juror.id, finalist.id);
            var fact = new HouseFactState { id = "fact-pact", kind = FactKinds.Alliance, refId = pact.id, visibility = FactVisibility.Private,
                knowers = new List<string> { juror.id, finalist.id } };
            s.story.facts.Add(fact);
            Assert.That(FinalistRead.Read(s, finalist.id).jurors.Single(j => j.jurorId == juror.id).lean, Is.EqualTo(FinalistRead.Uncertain),
                "A pact only its members know of is not the player's evidence.");
            Assert.That(FinalistRead.Read(s, finalist.id).alliances.value, Is.EqualTo("None known"));

            fact.knowers.Add(s.playerId);
            var heard = FinalistRead.Read(s, finalist.id);
            var lean = heard.jurors.Single(j => j.jurorId == juror.id);
            Assert.That(lean.lean, Is.EqualTo(FinalistRead.Support));
            Assert.That(lean.certainty, Is.EqualTo(FinalistRead.Suspected), "Heard of, not seen.");
            Assert.That(heard.support.value, Does.StartWith("1 juror: ").And.Contain("shared an alliance"));
            Assert.That(heard.alliances.certainty, Is.EqualTo(FinalistRead.Suspected));

            fact.visibility = FactVisibility.Public;
            Assert.That(FinalistRead.Read(s, finalist.id).jurors.Single(j => j.jurorId == juror.id).certainty, Is.EqualTo(FinalistRead.Confirmed));
        }

        [Test]
        public void AnAllianceWithThePlayerIsConfirmedAndNamedAndHowItEndedDoesNotChangeTheLean()
        {
            var s = FinalThree();
            var finalist = Finalist(s, 0); var juror = Juror(s, 0);
            var ours = Alliance(s, "ours", true, s.playerId, finalist.id, juror.id);
            var read = FinalistRead.Read(s, finalist.id);
            Assert.That(read.alliances.value, Is.EqualTo("With you: " + ours.name));
            Assert.That(read.alliances.certainty, Is.EqualTo(FinalistRead.Confirmed));
            Assert.That(read.jurors.Single(j => j.jurorId == juror.id).certainty, Is.EqualTo(FinalistRead.Confirmed));

            // Why a pact ended is worked out from scores the player never sees: it cannot move the read.
            ours.active = false;
            var row = new AllianceRow { id = ours.id, why = "player/left-house", startedWeek = 2, endedWeek = 6 };
            s.ledger.alliances.Add(row);
            var ended = FinalistRead.Read(s, finalist.id).jurors.Single(j => j.jurorId == juror.id);
            Assert.That(ended.lean, Is.EqualTo(FinalistRead.Support));
            foreach (var why in new[] { "player/turned", "player/soured", "npc/soured" })
            {
                row.why = why;
                var lean = FinalistRead.Read(s, finalist.id).jurors.Single(j => j.jurorId == juror.id);
                Assert.That(lean.lean, Is.EqualTo(ended.lean), why);
                Assert.That(lean.certainty, Is.EqualTo(ended.certainty), why);
            }
        }

        [Test]
        public void TheAgreementRowShowsWhatBindsBeforeWhatLapsed()
        {
            var s = FinalThree();
            var kept = Finalist(s, 0); var other = Finalist(s, 1);
            string keptFirst = kept.name.Split(' ')[0];
            s.deals.Add(new DealState { id = "old-offer", type = DealKind.FinalTwo, proposerId = kept.id, recipientId = s.playerId, status = DealStatus.Expired, week = 5 });
            Assert.That(FinalistRead.Read(s, kept.id).agreement.value, Is.EqualTo("Deal: lapsed"));
            Assert.That(FinalistRead.Read(s, kept.id).agreement.certainty, Is.EqualTo(FinalistRead.Confirmed), "The player was party to it.");
            Assert.That(FinalistRead.IfYouTake(s, kept.id, other.id).Any(line => line.Contains("Final 2 deal")), Is.False,
                "A lapsed deal binds nothing.");

            s.promises.Add(new PromiseState { id = "my-word", fromId = s.playerId, toId = kept.id, kind = PromiseKind.FinalTwo, status = PromiseStatus.Active, week = 8 });
            Assert.That(FinalistRead.Read(s, kept.id).agreement.value, Is.EqualTo("You promised: still standing"),
                "The promise the final eviction settles, not the deal that lapsed.");
            Assert.That(FinalistRead.IfYouTake(s, kept.id, other.id), Does.Contain("Honours your Final 2 deal with " + keptFirst + "."));
            Assert.That(FinalistRead.IfYouTake(s, other.id, kept.id), Does.Contain("Breaks your Final 2 deal with " + keptFirst + "."));

            var broken = FinalThree(32);
            var a = Finalist(broken, 0); var b = Finalist(broken, 1);
            broken.deals.Add(new DealState { id = "broken", type = DealKind.FinalTwo, proposerId = broken.playerId, recipientId = a.id, status = DealStatus.Broken, week = 6 });
            Assert.That(FinalistRead.Read(broken, a.id).agreement.value, Is.EqualTo("Deal: broken"));
            Assert.That(FinalistRead.IfYouTake(broken, a.id, b.id).Concat(FinalistRead.IfYouTake(broken, b.id, a.id)).Any(line => line.Contains("Final 2 deal")), Is.False);
        }

        [Test]
        public void TheFinalistCutIsCountedInTheJuryThatWillVote()
        {
            var s = FinalThree();
            var take = Finalist(s, 0); var cut = Finalist(s, 1);
            s.ledger.power.Add(new PowerRow { week = 5, hohId = take.id, nominees = new List<string> { cut.id, s.playerId } });
            Assert.That(FinalistRead.Read(s, take.id).bitterness.value, Is.EqualTo("None known"), "The card is today's jury; the cut finalist is not on it yet.");
            string takeFirst = take.name.Split(' ')[0], cutFirst = cut.name.Split(' ')[0];
            var lines = FinalistRead.IfYouTake(s, take.id, cut.id);
            Assert.That(lines, Does.Contain("1 juror has reason to hold a grudge against " + takeFirst + "."), "Cut, they vote.");
            Assert.That(lines, Does.Contain(cutFirst + " joins the jury with reason to hold a grudge against " + takeFirst + " (nominated them)."));
            Assert.That(lines.Any(line => line.StartsWith("You know little")), Is.False);
            Assert.That(FinalistRead.IfYouTake(s, cut.id, take.id), Does.Contain(takeFirst + " joins the jury."));
        }

        [Test]
        public void ALieExposedAtTheRevealIsAProvenVote()
        {
            var s = FinalThree();
            var finalist = Finalist(s, 0); var juror = Juror(s, 0); var spared = Juror(s, 1);
            // Week 4's block was the juror and another; the finalist said they would vote out the
            // other one, and the reveal showed the ballot went to the juror.
            s.ledger.power.Add(new PowerRow { week = 4, hohId = s.playerId, nominees = new List<string> { juror.id, spared.id }, evicteeId = juror.id, tally = new List<int> { 2, 1 } });
            s.ledger.claims.Add(new ClaimRow { week = 4, voterId = finalist.id, targetId = spared.id, source = ClaimSource.Told, status = ClaimStatus.Lied });
            var read = FinalistRead.Read(s, finalist.id);
            var lean = read.jurors.Single(j => j.jurorId == juror.id);
            Assert.That(lean.lean, Is.EqualTo(FinalistRead.Bitter));
            Assert.That(lean.certainty, Is.EqualTo(FinalistRead.Confirmed));
            Assert.That(lean.reason, Is.EqualTo("voted to evict them"));
            Assert.That(read.jurors.Single(j => j.jurorId == spared.id).lean, Is.EqualTo(FinalistRead.Uncertain), "The claimed target was not the one voted out.");
            s.ledger.claims[0].status = ClaimStatus.Open;
            Assert.That(FinalistRead.Read(s, finalist.id).jurors.Single(j => j.jurorId == juror.id).lean, Is.EqualTo(FinalistRead.Uncertain), "An open claim proves nothing.");
        }

        [Test]
        public void AgreementsWithJurorsEndBrokenEitherWay()
        {
            var s = FinalThree();
            var juror = Juror(s, 0); var promised = Juror(s, 1); var finalist = Finalist(s, 0);
            Assert.That(FinalistRead.BrokenEitherWay(s), Is.Empty);
            s.deals.Add(new DealState { id = "juror-deal", type = DealKind.FinalTwo, proposerId = juror.id, recipientId = s.playerId, status = DealStatus.Active, week = 4 });
            s.promises.Add(new PromiseState { id = "juror-word", fromId = s.playerId, toId = promised.id, kind = PromiseKind.FinalTwo, status = PromiseStatus.Active, week = 5 });
            s.promises.Add(new PromiseState { id = "their-word", fromId = Juror(s, 2).id, toId = s.playerId, kind = PromiseKind.FinalTwo, status = PromiseStatus.Active, week = 5 });
            s.deals.Add(new DealState { id = "finalist-deal", type = DealKind.FinalTwo, proposerId = s.playerId, recipientId = finalist.id, status = DealStatus.Active, week = 7 });
            Assert.That(FinalistRead.BrokenEitherWay(s), Is.EqualTo(new[] { juror.name.Split(' ')[0], promised.name.Split(' ')[0] }),
                "The engine settles the Head of Household's binding deals and own promises; a finalist's is the choice's.");
        }

        [Test]
        public void ThePublicRecordShowsWhoPutAJurorOnTheBlock()
        {
            var s = FinalThree();
            var finalist = Finalist(s, 0); var nominated = Juror(s, 0); var saved = Juror(s, 1); var clear = Juror(s, 2);
            // Week 3: nominated and voted out. Week 5: one saved by the veto (nominated all the same),
            // and one left on the final block beside the replacement.
            s.ledger.power.Add(new PowerRow { week = 3, hohId = finalist.id, nominees = new List<string> { nominated.id, s.playerId },
                evicteeId = nominated.id, tally = new List<int> { 3, 1 } });
            s.ledger.power.Add(new PowerRow { week = 5, hohId = finalist.id, vetoUsed = true, savedId = saved.id, replacementId = s.playerId,
                nominees = new List<string> { s.playerId, clear.id } });
            var read = FinalistRead.Read(s, finalist.id);
            foreach (var juror in new[] { nominated, saved, clear })
            {
                var lean = read.jurors.Single(j => j.jurorId == juror.id);
                Assert.That(lean.lean, Is.EqualTo(FinalistRead.Bitter), juror.name);
                Assert.That(lean.certainty, Is.EqualTo(FinalistRead.Confirmed), "A ceremony is public.");
                Assert.That(lean.reason, Is.EqualTo("nominated them"));
            }
            Assert.That(read.bitterness.value, Does.StartWith("3 jurors: "));
            var other = Finalist(s, 1);
            Assert.That(FinalistRead.IfYouTake(s, finalist.id, other.id), Does.Contain("3 jurors have reason to hold a grudge against " + finalist.name.Split(' ')[0] + "."));
            Assert.That(FinalistRead.Read(s, other.id).bitterness.value, Is.EqualTo("None known"), "Somebody else's ceremonies say nothing about them.");
        }

        [Test]
        public void AKeptClaimIsAConfirmedVoteAndALearnedStandingIsOnlySuspected()
        {
            var s = FinalThree();
            var finalist = Finalist(s, 0); var voted = Juror(s, 0); var warm = Juror(s, 1); var missed = Juror(s, 2);
            s.ledger.claims.Add(new ClaimRow { week = 4, voterId = finalist.id, targetId = voted.id, source = ClaimSource.Told, status = ClaimStatus.Kept });
            s.ledger.standings.Add(new StandingRow { week = 2, fromId = warm.id, toId = finalist.id, source = ClaimSource.Overheard, score = 40 });
            s.ledger.standings.Add(new StandingRow { week = 6, fromId = missed.id, toId = finalist.id, source = ClaimSource.Missed, score = -60 });
            var read = FinalistRead.Read(s, finalist.id);
            var byVote = read.jurors.Single(j => j.jurorId == voted.id);
            Assert.That(byVote.lean, Is.EqualTo(FinalistRead.Bitter)); Assert.That(byVote.certainty, Is.EqualTo(FinalistRead.Confirmed));
            Assert.That(byVote.reason, Is.EqualTo("voted to evict them"));
            var byStanding = read.jurors.Single(j => j.jurorId == warm.id);
            Assert.That(byStanding.lean, Is.EqualTo(FinalistRead.Support)); Assert.That(byStanding.certainty, Is.EqualTo(FinalistRead.Suspected));
            Assert.That(read.jurors.Single(j => j.jurorId == missed.id).lean, Is.EqualTo(FinalistRead.Uncertain), "A missed read taught the player nothing.");
            Assert.That(read.support.certainty, Is.EqualTo(FinalistRead.Suspected));
            Assert.That(read.bitterness.certainty, Is.EqualTo(FinalistRead.Confirmed));
        }

        [Test]
        public void EvidenceBothWaysIsUncertain()
        {
            var s = FinalThree();
            var finalist = Finalist(s, 0); var juror = Juror(s, 0);
            Alliance(s, "ours", true, s.playerId, finalist.id, juror.id);
            s.ledger.power.Add(new PowerRow { week = 3, hohId = finalist.id, nominees = new List<string> { juror.id, s.playerId } });
            var lean = FinalistRead.Read(s, finalist.id).jurors.Single(j => j.jurorId == juror.id);
            Assert.That(lean.lean, Is.EqualTo(FinalistRead.Uncertain));
            Assert.That(lean.reason, Is.EqualTo("mixed signals"));
        }

        [Test]
        public void TheRecordIsGradedAgainstTheHouse()
        {
            var s = FinalThree();
            var a = Finalist(s, 0); var b = Finalist(s, 1); var juror = Juror(s, 0);
            Assert.That(FinalistRead.Grade(s, a), Is.EqualTo(FinalistRead.Light));
            a.hohWins = 1;
            Assert.That(FinalistRead.Grade(s, a), Is.EqualTo(FinalistRead.Moderate));
            a.vetoWins = 1;
            Assert.That(FinalistRead.Grade(s, a), Is.EqualTo(FinalistRead.Strong), "Two wins and nobody has more.");
            juror.hohWins = 3;
            Assert.That(FinalistRead.Grade(s, a), Is.EqualTo(FinalistRead.Moderate), "Two wins when a juror has three.");
            s.finalPart1WinnerId = a.id;
            Assert.That(FinalistRead.Wins(s, a), Is.EqualTo(3));
            Assert.That(FinalistRead.Grade(s, a), Is.EqualTo(FinalistRead.Strong));
            a.timesNominated = 2;
            Assert.That(FinalistRead.Read(s, a.id).resume.value, Is.EqualTo("Strong · HoH 1 · Veto 1 · Nominated 2 · Won Final HoH Part 1"));
            Assert.That(FinalistRead.IfYouTake(s, a.id, b.id), Does.Contain(a.name.Split(' ')[0] + " sits beside you with a strong competition record."));
            Assert.That(FinalistRead.IfYouTake(s, b.id, a.id), Does.Contain(b.name.Split(' ')[0] + " has no competition wins to argue with."));
        }

        [Test]
        public void TheYourRelationshipWordIsThePlayersOwnReading()
        {
            var s = FinalThree();
            var finalist = Finalist(s, 0);
            SetScore(s, finalist.id, s.playerId, -90);
            foreach (var (score, word) in new[] { (40d, "Friendly"), (15d, "Friendly"), (14d, "Neutral"), (-15d, "Wary"), (-39d, "Wary"), (-40d, "Hostile") })
            {
                SetScore(s, s.playerId, finalist.id, score);
                Assert.That(FinalistRead.Read(s, finalist.id).relationship.value, Is.EqualTo(word), score.ToString());
            }
            Alliance(s, "ours", true, s.playerId, finalist.id);
            Assert.That(FinalistRead.Read(s, finalist.id).relationship.value, Is.EqualTo("Allied"));
        }

        [Test]
        public void TheJuryModelsOwnNumbersNeverAppearAndNothingIsChanged()
        {
            var s = FinalThree();
            var a = Finalist(s, 0); var b = Finalist(s, 1);
            // Numbers no card could print by accident: the model's inputs, planted.
            foreach (var juror in FinalistRead.Jurors(s))
            {
                SetScore(s, juror.id, a.id, -87);
                SetScore(s, juror.id, b.id, 93);
                s.story.grudges.Add(new GrudgeState { holderId = juror.id, targetId = a.id, cause = GrudgeCauses.DealBroken, severity = 77, originWeek = 5, count = 1 });
            }
            s.ledger.standings.Add(new StandingRow { week = 8, fromId = s.playerId, toId = a.id, source = ClaimSource.Read, score = 61 });
            string before = JsonConvert.SerializeObject(s);
            uint random = s.randomState;
            var texts = new List<string>();
            foreach (var (take, cut) in new[] { (a, b), (b, a) })
                texts.Add(AllText(FinalistRead.Read(s, take.id), FinalistRead.IfYouTake(s, take.id, cut.id)));
            string all = string.Join("\n", texts);
            foreach (var number in new[] { "87", "93", "77", "61" })
                Assert.That(all, Does.Not.Contain(number), "A model input on a card: " + number);
            foreach (var juror in FinalistRead.Jurors(s))
                foreach (var finalist in new[] { a, b })
                {
                    double score = WebJuryVoting.Score(s, juror.id, finalist.id);
                    if (System.Math.Abs(score) >= 10) Assert.That(all, Does.Not.Contain(System.Math.Round(score).ToString("0")), "The jury model's score on a card.");
                }
            Assert.That(FinalistRead.Read(s, a.id).bitterness.value, Is.EqualTo("None known"), "A grudge is the model's input, not the player's knowledge.");
            Assert.That(JsonConvert.SerializeObject(s), Is.EqualTo(before), "Reading the finalists must not change the state.");
            Assert.That(s.randomState, Is.EqualTo(random), "or draw from its generator.");
        }

        /// <summary>
        /// ENDGAME-PLAN F6: a juror's case says what the finalist did to the player, and a broken
        /// deal is the finalist's only where the record shows it was their act - the ceremony, the
        /// veto, the vote the player's own ballot kept. A deal the player broke is not held against
        /// the finalist, a voting block is both of theirs, and a promise counts only one way.
        /// </summary>
        [Test]
        public void AJurorsCaseCountsOnlyTheDealsTheFinalistBroke()
        {
            var s = FinalThree();
            var finalist = Finalist(s, 0); var other = Finalist(s, 1); var target = Juror(s, 0);
            s.Find(s.playerId).status = ContestantStatus.Jury;
            s.phase = EpisodePhase.Jury;
            s.hohId = finalist.id;
            string you = s.playerId, them = finalist.id;
            // Week 4: the player, as Head of Household, put the finalist up. Week 5: a vote on the
            // target. Week 6: the finalist put the player up. Week 7: the finalist held the veto and
            // left the player on the block.
            s.ledger.power.Add(new PowerRow { week = 4, hohId = you, nominees = new List<string> { them, target.id }, evicteeId = other.id, tally = new List<int> { 3, 1 } });
            s.ledger.power.Add(new PowerRow { week = 5, hohId = other.id, nominees = new List<string> { target.id, other.id }, evicteeId = target.id, tally = new List<int> { 3, 1 } });
            s.ledger.power.Add(new PowerRow { week = 6, hohId = them, nominees = new List<string> { you, target.id }, evicteeId = target.id, tally = new List<int> { 2, 1 } });
            s.ledger.power.Add(new PowerRow { week = 7, hohId = other.id, vetoHolderId = them, nominees = new List<string> { you, target.id }, evicteeId = you, tally = new List<int> { 2, 0 } });
            s.ledger.ballots.Add(new BallotRow { week = 5, voterId = you, targetId = target.id });
            DealState Deal(string id, string type, int week, int expires, string targetId = null)
            {
                var deal = new DealState { id = id, type = type, proposerId = you, recipientId = them, targetId = targetId, status = DealStatus.Broken, week = week, expiresWeek = expires };
                s.deals.Add(deal);
                return deal;
            }
            var yours = Deal("safety-yours", DealKind.SafetyAgreement, 4, 5);
            var theirs = Deal("safety-theirs", DealKind.SafetyAgreement, 6, 7);
            var veto = Deal("veto", DealKind.VetoUse, 7, 7);
            var evict = Deal("evict", DealKind.VoteEvict, 5, 5, target.id);
            var save = Deal("save", DealKind.VoteSave, 5, 5, target.id);
            var block = Deal("block", DealKind.VoteTogether, 5, 5);
            Assert.That(FinalistRead.BrokeADealWithYou(s, yours, them), Is.False, "The player broke it: they put the finalist up first.");
            Assert.That(FinalistRead.BrokeADealWithYou(s, theirs, them), Is.True, "The finalist put the player up.");
            Assert.That(FinalistRead.BrokeADealWithYou(s, veto, them), Is.True, "The finalist held the veto and left the player up.");
            Assert.That(FinalistRead.BrokeADealWithYou(s, evict, them), Is.True, "The player's own ballot kept it, so the finalist broke it.");
            Assert.That(FinalistRead.BrokeADealWithYou(s, save, them), Is.False, "The player's own ballot broke it.");
            Assert.That(FinalistRead.BrokeADealWithYou(s, block, them), Is.False, "A voting block is both of theirs.");
            s.promises.Add(new PromiseState { id = "their-word", fromId = them, toId = you, kind = PromiseKind.FinalTwo, status = PromiseStatus.Broken, week = 5 });
            s.promises.Add(new PromiseState { id = "your-word", fromId = you, toId = them, kind = PromiseKind.FinalTwo, status = PromiseStatus.Broken, week = 5 });

            var fact = FinalistRead.JurorCase(s, them).Facts.Last();
            Assert.That(fact.label, Is.EqualTo("What they did to you"));
            Assert.That(fact.certainty, Is.EqualTo(FinalistRead.Confirmed));
            Assert.That(fact.value, Is.EqualTo("Week 6: nominated you · Broke 3 deals with you · Your voting block fell apart · Broke a promise to you"));
            Assert.That(FinalistRead.TowardYou(s, other.id).value, Is.EqualTo("Week 7: nominated you"),
                "The other finalist's own week is theirs, and none of the deals, the block or the promises are.");
        }

        /// <summary>
        /// MOCKUP-PASS M8: the line under the player's standing on a finalist's card is the record
        /// between the two of them - the alliance and the week it began, the deals kept, what the
        /// finalist did to the player - and nothing of anybody else's.
        /// </summary>
        [Test]
        public void TheStandingLineIsTheRecordBetweenThem()
        {
            var s = FinalThree();
            var finalist = Finalist(s, 0); var other = Finalist(s, 1);
            Assert.That(FinalistRead.RelationshipLine(s, finalist.id), Is.Null, "Nothing on the record between them.");
            var ours = Alliance(s, "ours", true, s.playerId, finalist.id);
            Assert.That(FinalistRead.RelationshipLine(s, finalist.id), Is.Null, "An alliance with no ledger row says nothing the standing word does not.");
            s.ledger.alliances.Add(new AllianceRow { id = ours.id, startedWeek = 3, why = "player/formed" });
            Assert.That(FinalistRead.RelationshipLine(s, finalist.id), Is.EqualTo("Allied since week 3"));
            s.deals.Add(new DealState { id = "kept", type = DealKind.SafetyAgreement, proposerId = finalist.id, recipientId = s.playerId, status = DealStatus.Fulfilled, week = 4 });
            s.ledger.power.Add(new PowerRow { week = 6, hohId = finalist.id, nominees = new List<string> { s.playerId, other.id } });
            Assert.That(FinalistRead.RelationshipLine(s, finalist.id), Is.EqualTo("Allied since week 3 · Kept a deal with you · Week 6: nominated you"));

            // Their alliance with somebody else, and a deal they kept with somebody else, are not the player's record.
            var theirs = Alliance(s, "theirs", true, finalist.id, other.id);
            s.ledger.alliances.Add(new AllianceRow { id = theirs.id, startedWeek = 1, why = "npc/formed" });
            s.deals.Add(new DealState { id = "theirs", type = DealKind.SafetyAgreement, proposerId = finalist.id, recipientId = other.id, status = DealStatus.Fulfilled, week = 2 });
            Assert.That(FinalistRead.RelationshipLine(s, finalist.id), Is.EqualTo("Allied since week 3 · Kept a deal with you · Week 6: nominated you"));
            ours.active = false;
            Assert.That(FinalistRead.RelationshipLine(s, finalist.id), Is.EqualTo("Kept a deal with you · Week 6: nominated you"), "An alliance that ended is not one they are in.");
            Assert.That(FinalistRead.Read(s, finalist.id).standingLine, Is.EqualTo(FinalistRead.RelationshipLine(s, finalist.id)));
            Assert.That(FinalistRead.RelationshipLine(s, s.playerId), Is.Null, "Nobody has a record with themselves.");
        }

        /// <summary>
        /// The card's bar is the player's own score, as the cast strip draws it, never the
        /// finalist's view of the player; the parts they won are on the card; every juror is named
        /// by their first name.
        /// </summary>
        [Test]
        public void TheCardsCountsAreThePlayersOwnAndTheRecords()
        {
            var s = FinalThree();
            var finalist = Finalist(s, 0);
            SetScore(s, s.playerId, finalist.id, 37);
            SetScore(s, finalist.id, s.playerId, -80);
            var read = FinalistRead.Read(s, finalist.id);
            Assert.That(read.standing, Is.EqualTo(37), "The player's reading of them.");
            Assert.That(read.finalPartsWon, Is.Empty);
            s.finalPart2WinnerId = finalist.id;
            Assert.That(FinalistRead.Read(s, finalist.id).finalPartsWon, Is.EqualTo(new[] { 2 }));
            s.finalPart1WinnerId = finalist.id;
            Assert.That(FinalistRead.Read(s, finalist.id).finalPartsWon, Is.EqualTo(new[] { 1, 2 }));
            foreach (var juror in FinalistRead.Read(s, finalist.id).jurors)
                Assert.That(juror.name, Is.EqualTo(FinalistRead.FirstName(s.Find(juror.jurorId).name)));
        }

        /// <summary>
        /// Decision 32's tile: the eviction votes a finalist sat on the block through and stayed. A
        /// veto save, a week with no vote on the record and the final eviction are not votes survived.
        /// </summary>
        [Test]
        public void VotesSurvivedAreTheVotesTheySatThroughOnTheBlock()
        {
            var s = FinalThree();
            var finalist = Finalist(s, 0); var gone = Juror(s, 0); var saved = Juror(s, 1); var other = Finalist(s, 1);
            Assert.That(FinalistRead.VotesSurvived(s, finalist.id), Is.Zero);
            s.ledger.power.Add(new PowerRow { week = 2, hohId = saved.id, nominees = new List<string> { finalist.id, gone.id }, evicteeId = gone.id, tally = new List<int> { 3, 1 } });
            s.ledger.power.Add(new PowerRow { week = 3, hohId = gone.id, vetoUsed = true, savedId = finalist.id, replacementId = saved.id,
                nominees = new List<string> { saved.id, other.id }, evicteeId = saved.id, tally = new List<int> { 2, 1 } });
            s.ledger.power.Add(new PowerRow { week = 4, hohId = other.id, nominees = new List<string> { finalist.id, s.playerId } });
            s.ledger.power.Add(new PowerRow { week = 5, hohId = s.playerId, nominees = new List<string> { finalist.id, other.id }, evicteeId = finalist.id, tally = new List<int> { 1, 2 } });
            Assert.That(FinalistRead.VotesSurvived(s, finalist.id), Is.EqualTo(1),
                "Week 2 only: saved by the veto in week 3, no vote on the record in week 4, and week 5's row names them the evictee.");
            Assert.That(FinalistRead.Read(s, finalist.id).votesSurvived, Is.EqualTo(1));
            Assert.That(FinalistRead.VotesSurvived(s, other.id), Is.EqualTo(2), "Week 3's final block, and week 5's.");
        }

        /// <summary>
        /// Decision 33: the crown on a juror's case marks the final Head of Household, read from the
        /// final eviction's own row, and nobody before that choice is made.
        /// </summary>
        [Test]
        public void TheFinalHeadOfHouseholdIsCrownedOnlyOnceTheyHaveChosen()
        {
            var s = FinalThree();
            var a = Finalist(s, 0); var b = Finalist(s, 1);
            s.ledger.power.Add(new PowerRow { week = 7, hohId = b.id, nominees = new List<string> { a.id, Juror(s, 0).id }, evicteeId = Juror(s, 0).id, tally = new List<int> { 2, 0 } });
            Assert.That(FinalistRead.FinalHeadOfHousehold(s), Is.Null, "Before the choice nobody is crowned.");
            s.ledger.power.Add(new PowerRow { week = 9, hohId = a.id, nominees = new List<string> { s.playerId, b.id }, evicteeId = b.id });
            Assert.That(FinalistRead.FinalHeadOfHousehold(s), Is.Null, "Not while the season still stands at the final eviction.");
            s.phase = EpisodePhase.Jury;
            Assert.That(FinalistRead.FinalHeadOfHousehold(s), Is.EqualTo(a.id));
            Assert.That(FinalistRead.Read(s, a.id).finalHead, Is.True);
            Assert.That(FinalistRead.Read(s, b.id).finalHead, Is.False, "Holding the house in week 7 is not the final crown.");
        }

#if UNITY_5_3_OR_NEWER
        /// <summary>The simulation's word for the player's standing is the relationship web's, threshold for threshold.</summary>
        [Test]
        public void TheStandingWordIsTheRelationshipWebs()
        {
            var s = FinalThree();
            var finalist = Finalist(s, 0);
            foreach (double score in new[] { 60d, 40d, 15d, 14.9d, 0d, -14.9d, -15d, -39.9d, -40d, -80d })
            {
                SetScore(s, s.playerId, finalist.id, score);
                Assert.That(FinalistRead.StandingWord(s, finalist.id),
                    Is.EqualTo(RelationshipWeb.StandingWord(RelationshipWeb.KindOf(s, finalist.id))), score.ToString());
            }
            Alliance(s, "ours", true, s.playerId, finalist.id);
            Assert.That(FinalistRead.StandingWord(s, finalist.id), Is.EqualTo(RelationshipWeb.StandingWord(RelationshipWeb.KindOf(s, finalist.id))));
            Assert.That(FinalistRead.FriendThreshold, Is.EqualTo(RelationshipWeb.FriendThreshold));
            Assert.That(FinalistRead.DistrustThreshold, Is.EqualTo(RelationshipWeb.DistrustThreshold));
            Assert.That(FinalistRead.RivalThreshold, Is.EqualTo(RelationshipWeb.RivalThreshold));
        }
#endif
    }
}
