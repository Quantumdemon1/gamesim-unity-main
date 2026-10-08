using System.Collections.Generic;
using System.Linq;
using Gamesim.Simulation;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// The war rooms' deferred readers (WAVE-D-NPC-PACTS-PLAN D3-S7): the week's review (<see cref="YourWeek"/>),
    /// the notes on a houseguest (<see cref="HouseguestNotes"/>), the Your word page (<see cref="CommitmentsRead"/>),
    /// Game Sense's copy, and the campaign's goals and talking points (<see cref="CampaignBrief"/>), which offered a
    /// call the engine refuses a pact of three. Each says a war room's plan as a plan, in what the player was told:
    /// the says at the meeting, the answer's line, and the ballots the player can place - never a dissenter
    /// flagged, and never a coin the player was not told. With no plan on the ledger every reader reads as
    /// it always did. Unity-free, so the dotnet subset runs it (Tools/SimulationTests).
    /// </summary>
    public sealed class PactPlanReaderTests
    {
        private const string PactId = PactPlanTests.PactId, PactName = PactPlanTests.PactName;

        // ------------------------------------------------------------ YourWeek

        [Test]
        public void YourWeekSaysAPlanTheWayThePlayerAnsweredItAndItsVotersAsTheyWereTold()
        {
            var open = PactPlanTests.Opened(out _);
            var npcs = NpcIds(open);
            var waiting = YourWeek.Build(open, open.week).calls;
            Assert.That(waiting.Select(l => (l.kind, l.verdict, l.text)),
                Is.EqualTo(new[] { (YourWeek.Kinds.Plan, (string)null, "You have not answered " + PactName + "'s plan yet.") }), "An open plan: its line and nobody judged.");

            var agreed = Answer(open, npcs[3], npcs[1]);
            var calls = YourWeek.Build(agreed, agreed.week).calls;
            Assert.That(calls.Any(l => l.kind == YourWeek.Kinds.Call), Is.False, "The call the plan made is said as its plan,");
            Assert.That(calls[0].kind, Is.EqualTo(YourWeek.Kinds.Plan));
            Assert.That(calls[0].verdict, Is.Null, "which carries no verdict of its own;");
            Assert.That(calls[0].text, Is.EqualTo("You went with " + PactName + "'s plan: evict " + Name(agreed, npcs[1]) + "."));
            var members = calls.Skip(1).ToList();
            Assert.That(members.Select(l => l.aboutId), Is.EqualTo(new[] { npcs[3], npcs[4] }), "its voters, in the order they met,");
            Assert.That(members.Select(l => (l.kind, l.verdict, l.basis)), Is.All.EqualTo((YourWeek.Kinds.Member, YourWeek.Verdicts.Followed, YourWeek.Bases.Call)),
                "each with it as the answer's line said, before the reveal.");
            Assert.That(members[0].text, Is.EqualTo(Name(agreed, npcs[3]) + " was with the plan."));

            // A voter the answer's line said was not with it is not known until their ballot is - never defected.
            var dissent = agreed.Clone();
            dissent.ledger.plans[0].followed = new List<string> { npcs[3] };
            dissent.ledger.calls[0].followed = new List<string> { npcs[3] };
            var not = YourWeek.Build(dissent, dissent.week).calls.Single(l => l.aboutId == npcs[4]);
            Assert.That((not.verdict, not.basis, not.text), Is.EqualTo((YourWeek.Verdicts.NotKnown, YourWeek.Bases.Call, Name(dissent, npcs[4]) + " was not with the plan.")));

            // The reveal: the plan's ending, and each voter whose ballot the player can place judged by it.
            var revealed = Reveal(agreed, npcs[1]);
            var power = revealed.ledger.power.Last(p => p.week == agreed.week);
            var after = YourWeek.Build(revealed, agreed.week).calls;
            string target = Name(revealed, npcs[1]);
            Assert.That(after[0].text, Is.EqualTo("You went with " + PactName + "'s plan: evict " + target
                + (power.evicteeId == npcs[1] ? ", and " + target + " went home." : ", and " + target + " stayed.")));
            var sheet = KnownBallots.Read(revealed, agreed.week);
            foreach (var line in after.Skip(1))
            {
                string ballot = sheet.TargetOf(line.aboutId);
                if (ballot == null) { Assert.That((line.verdict, line.basis), Is.EqualTo((YourWeek.Verdicts.Followed, YourWeek.Bases.Call)), line.text); continue; }
                Assert.That(line.basis, Is.EqualTo(YourWeek.Bases.Ballot), line.text);
                Assert.That(line.verdict, Is.EqualTo(ballot == npcs[1] ? YourWeek.Verdicts.Followed : YourWeek.Verdicts.Defected), line.text);
                Assert.That(line.text, Is.EqualTo(Name(revealed, line.aboutId) + " was with the plan"
                    + (ballot == npcs[1] ? ", and voted out " + target + "." : ", then voted to evict " + Name(revealed, ballot) + ".")));
            }
            Assert.That(after.Skip(1).Count(l => l.basis == YourWeek.Bases.Ballot), Is.GreaterThan(0), "Precondition: the player can place a ballot of the plan's.");
            Assert.That(string.Join(" ", after.Select(l => l.text)), Does.Not.Match("[0-9]"), "Words, never a number.");

            // Without a plan on the ledger the same call reads as the call it always was.
            var off = agreed.Clone();
            off.ledger.plans.Clear();
            var call = YourWeek.Build(off, off.week).calls;
            Assert.That(call[0].kind, Is.EqualTo(YourWeek.Kinds.Call));
            Assert.That(call[0].text, Is.EqualTo("You called it in " + PactName + ": evict " + Name(off, npcs[1]) + "."));
            Assert.That(call.Skip(1).Select(l => l.text), Is.EqualTo(new[] { Name(off, npcs[3]) + " was with you at the call.", Name(off, npcs[4]) + " was with you at the call." }));
        }

        /// <summary>A plan an NPC leads: a dissenter who went along on their own coin was never said, so reads the same either way (the knowledge gate).</summary>
        [Test]
        public void YourWeekNeverTellsTheCoinADissenterWentAlongOn()
        {
            var open = PactPlanTests.Opened(out _, split: true);
            var npcs = NpcIds(open);
            var low = Answer(open, npcs[3], null);
            var row = low.ledger.plans.Single();
            Assert.That((row.stance, row.targetId, row.callerId), Is.EqualTo((PactPlanStance.Low, npcs[1], npcs[3])), "Precondition: on a split, the first say; an NPC leads.");
            var lines = new List<List<string>>();
            foreach (bool along in new[] { true, false })
            {
                var copy = low.Clone();
                copy.ledger.plans[0].followed = along ? new List<string> { npcs[3], npcs[4] } : new List<string> { npcs[3] };
                var calls = YourWeek.Build(copy, copy.week).calls;
                Assert.That(calls[0].text, Is.EqualTo("You lay low on " + PactName + "'s plan: evict " + Name(copy, npcs[1]) + "."));
                var dissenter = calls.Single(l => l.aboutId == npcs[4]);
                Assert.That((dissenter.verdict, dissenter.text), Is.EqualTo((YourWeek.Verdicts.NotKnown, Name(copy, npcs[4]) + " did not back the plan.")));
                Assert.That(calls.Single(l => l.aboutId == npcs[3]).verdict, Is.EqualTo(YourWeek.Verdicts.Followed), "The one who wanted it, as its line said.");
                lines.Add(calls.Select(l => l.ToString()).ToList());
                Assert.That(HouseguestNotes.For(copy, npcs[4]).Select(n => n.ToString()), Is.EqualTo(HouseguestNotes.For(low, npcs[4]).Select(n => n.ToString())),
                    "Nor do the notes on them.");
            }
            Assert.That(lines[0], Is.EqualTo(lines[1]), "The page reads the same whichever way the coin fell.");
        }

        /// <summary>
        /// A dissenter whose ballot the player can place, under a plan an NPC leads: the alliances page's twin of
        /// the test above (<see cref="AllianceRead.PlanText"/>) - the coin they went along on was never said, so
        /// their ballot is no follow-through of the plan's and the page reads the same either way - and the week's
        /// review judges the ballot without flagging them: they voted as they said, so no verdict, never defected.
        /// </summary>
        [Test]
        public void ADissentersKnownBallotNeverTellsTheCoinNorFlagsThem()
        {
            var open = PactPlanTests.Opened(out _, split: true);
            var npcs = NpcIds(open);
            var revealed = Reveal(Answer(open, npcs[3], null), npcs[1]);
            var row = revealed.ledger.plans.Single();
            Assert.That((row.stance, row.targetId, row.callerId), Is.EqualTo((PactPlanStance.Low, npcs[1], npcs[3])), "Precondition: on a split, the first say; an NPC leads.");
            // The dissenter told the player their vote and kept their word: a ballot the player can place.
            string cast = revealed.votes.Single(v => v.voterId == npcs[4]).targetId;
            Assert.That(cast, Is.Not.EqualTo(npcs[1]), "Precondition: the dissenter voted the other way, as they said.");
            revealed.ledger.claims.Add(new ClaimRow { week = row.week, voterId = npcs[4], targetId = cast, source = ClaimSource.Told, status = ClaimStatus.Kept });
            Assert.That(KnownBallots.Read(revealed, row.week).TargetOf(npcs[4]), Is.EqualTo(cast), "Precondition: the player can place the dissenter's ballot.");
            var texts = new List<string>();
            var reviews = new List<List<string>>();
            foreach (bool along in new[] { true, false })
            {
                var copy = revealed.Clone();
                copy.ledger.plans[0].followed = along ? new List<string> { npcs[3], npcs[4] } : new List<string> { npcs[3] };
                texts.Add(AllianceRead.Read(copy).yours.Single(p => p.id == PactId).plans.Single().text);
                var calls = YourWeek.Build(copy, row.week).calls;
                var dissenter = calls.Single(l => l.aboutId == npcs[4]);
                Assert.That((dissenter.verdict, dissenter.basis, dissenter.text), Is.EqualTo(((string)null, YourWeek.Bases.Ballot,
                    Name(copy, npcs[4]) + " did not back the plan, and voted to evict " + Name(copy, cast) + ".")), "Judged by the ballot, never flagged.");
                reviews.Add(calls.Select(l => l.ToString()).ToList());
            }
            Assert.That(texts[0], Is.EqualTo(texts[1]), "The page reads the same whichever way the coin fell,");
            string answered = texts[0].Substring(texts[0].IndexOf(" You lay low: ", System.StringComparison.Ordinal));
            Assert.That(answered, Does.Not.Contain(FinalistRead.FirstName(Name(revealed, npcs[4]))), "and never says the dissenter's ballot as the plan's;");
            Assert.That(reviews[0], Is.EqualTo(reviews[1]), "nor does the week's review.");
        }

        [Test]
        public void APlansLineSaysEveryStanceInWords()
        {
            var open = PactPlanTests.Opened(out _);
            var npcs = NpcIds(open);
            string m = Name(open, npcs[1]), c = Name(open, npcs[2]);
            PactPlanRow Settled(string stance, string target, string caller, string counter = null)
            {
                var row = open.ledger.plans.Single().Clone();
                row.stance = stance; row.targetId = target; row.callerId = caller; row.counterId = counter;
                return row;
            }
            string Line(PactPlanRow row, PowerRow power = null) => YourWeek.PlanLine(open, row, power, power != null);
            Assert.That(Line(Settled(PactPlanStance.Countered, npcs[2], open.playerId, npcs[2])), Is.EqualTo(PactName + " went with your push: evict " + c + "."));
            Assert.That(Line(Settled(PactPlanStance.Countered, npcs[1], npcs[3], npcs[2])), Is.EqualTo("You pushed for " + c + ", but " + PactName + " held to its plan: evict " + m + "."));
            Assert.That(Line(Settled(PactPlanStance.Low, npcs[1], npcs[3])), Is.EqualTo("You lay low on " + PactName + "'s plan: evict " + m + "."));
            Assert.That(Line(Settled(PactPlanStance.Lapsed, npcs[1], npcs[3])), Is.EqualTo("You let " + PactName + "'s plan stand: evict " + m + "."));
            Assert.That(Line(Settled(PactPlanStance.Void, null, null)), Is.EqualTo(PactName + "'s plan came to nothing."));
            var went = new PowerRow { week = open.week, evicteeId = npcs[1] };
            var stayed = new PowerRow { week = open.week, evicteeId = npcs[2] };
            Assert.That(Line(Settled(PactPlanStance.Lapsed, npcs[1], npcs[3]), went), Does.EndWith("evict " + m + ", and " + m + " went home."));
            Assert.That(Line(Settled(PactPlanStance.Lapsed, npcs[1], npcs[3]), stayed), Does.EndWith("evict " + m + ", and " + m + " stayed."));
            Assert.That(Line(Settled(PactPlanStance.Low, open.playerId, npcs[3]), stayed), Is.EqualTo("You lay low on " + PactName + "'s plan: evict you, and you stayed."),
                "The player as the plan reads as the player.");
            Assert.That(string.Join(" ", YourWeek.Build(open, open.week).calls.Concat(YourWeek.Build(Answer(open, npcs[3], npcs[2]), open.week).calls).Select(l => l.text)),
                Does.Not.Match("[0-9]"), "Words, never a number.");
        }

        // ------------------------------------------------------------ HouseguestNotes

        [Test]
        public void TheNotesSayWhatAMemberSaidAtTheMeetingAndWhetherTheyWereWithAPlanYouBacked()
        {
            var open = PactPlanTests.Opened(out _);
            var npcs = NpcIds(open);
            string first = FinalistRead.FirstName(Name(open, npcs[3])), target = Name(open, npcs[1]);
            var said = HouseguestNotes.For(open, npcs[3]).Single(n => n.kind == HouseguestNotes.Kinds.Vote);
            Assert.That((said.week, said.text, said.brief), Is.EqualTo((open.week, "At " + PactName + "'s meeting: wanted " + target + " out",
                "Wanted " + FinalistRead.FirstName(target) + " out")), "Their say, as the meeting's line told it.");

            var agreed = Answer(open, npcs[3], npcs[1]);
            var notes = HouseguestNotes.For(agreed, npcs[3]);
            var with = notes.Single(n => n.kind == HouseguestNotes.Kinds.Word);
            Assert.That((with.text, with.brief), Is.EqualTo((first + " was with the plan you backed to evict " + target, "With your plan")));
            Assert.That(notes.Any(n => n.text.Contains("your call")), Is.False, "The call the plan made is said as the plan,");
            var dissent = agreed.Clone();
            dissent.ledger.plans[0].followed = new List<string> { npcs[3] };
            dissent.ledger.calls[0].followed = new List<string> { npcs[3] };
            var not = HouseguestNotes.For(dissent, npcs[4]).Single(n => n.kind == HouseguestNotes.Kinds.Word);
            Assert.That((not.text, not.brief), Is.EqualTo((FinalistRead.FirstName(Name(dissent, npcs[4])) + " wasn't with the plan you backed to evict " + target, "Not with your plan")),
                "and one not with it is never said to have ignored a call.");

            // Without a plan on the ledger, the call it made reads as it always did.
            var off = agreed.Clone();
            off.ledger.plans.Clear();
            var plain = HouseguestNotes.For(off, npcs[3]);
            Assert.That(plain.Single(n => n.kind == HouseguestNotes.Kinds.Word).text, Is.EqualTo(first + " followed your call to evict " + target));
            Assert.That(plain.Any(n => n.text.StartsWith("At ")), Is.False);

            // The reveal: how they voted, once the player can place it.
            var revealed = Reveal(agreed, npcs[1]);
            var sheet = KnownBallots.Read(revealed, agreed.week);
            foreach (string id in new[] { npcs[3], npcs[4] })
            {
                var note = HouseguestNotes.For(revealed, id).Single(n => n.kind == HouseguestNotes.Kinds.Vote && n.week == agreed.week);
                string ballot = sheet.TargetOf(id);
                Assert.That(note.text, Is.EqualTo("At " + PactName + "'s meeting: wanted " + target + " out"
                    + (ballot == null ? "" : ballot == npcs[1] ? " · and voted that way" : " · voted to evict " + Name(revealed, ballot))));
            }
        }

        [Test]
        public void TheNotesSayWhoCameRoundToYourPush()
        {
            var open = PactPlanTests.Opened(out _);
            var npcs = NpcIds(open);
            var row = open.ledger.plans[0];
            row.stance = PactPlanStance.Countered; row.counterId = npcs[2]; row.cameRound = new List<string> { npcs[3] };
            row.targetId = npcs[2]; row.callerId = open.playerId; row.followed = new List<string> { npcs[3] };
            Assert.That(PactPlans.FinalSay(row, npcs[3]), Is.EqualTo(npcs[2]));
            Assert.That(PactPlans.FinalSay(row, npcs[4]), Is.EqualTo(npcs[1]));
            var note = HouseguestNotes.For(open, npcs[3]).Single(n => n.kind == HouseguestNotes.Kinds.Vote);
            Assert.That((note.text, note.brief), Is.EqualTo(("At " + PactName + "'s meeting: wanted " + Name(open, npcs[1]) + " out · came round to " + Name(open, npcs[2]),
                "Came round to " + FinalistRead.FirstName(Name(open, npcs[2])))));
            Assert.That(HouseguestNotes.For(open, npcs[4]).Single(n => n.kind == HouseguestNotes.Kinds.Vote).text, Does.Not.Contain("came round"));
        }

        // ------------------------------------------------------------ CommitmentsRead and Game Sense

        [Test]
        public void YourWordListsThoseWithAPlanYouBackedAndNeverADissenter()
        {
            var open = PactPlanTests.Opened(out _);
            var npcs = NpcIds(open);
            var agreed = Answer(open, npcs[3], npcs[1]);
            var calls = CommitmentsRead.Of(agreed).Where(c => c.kind == CommitmentsRead.Kinds.Call).ToList();
            Assert.That(calls.Select(c => c.withId), Is.EqualTo(new[] { npcs[3], npcs[4] }));
            Assert.That(calls.Select(c => c.title), Is.All.EqualTo("The plan you backed in " + PactName));
            Assert.That(calls.Select(c => (c.outcome, c.status)), Is.All.EqualTo((CommitmentsRead.Outcomes.Open, "with you")));
            var dissent = agreed.Clone();
            dissent.ledger.plans[0].followed = new List<string> { npcs[3] };
            dissent.ledger.calls[0].followed = new List<string> { npcs[3] };
            var listed = CommitmentsRead.Of(dissent).Where(c => c.kind == CommitmentsRead.Kinds.Call).ToList();
            Assert.That(listed.Select(c => c.withId), Is.EqualTo(new[] { npcs[3] }), "A dissenter never gave the plan its word, so is not on the page,");
            Assert.That(CommitmentsRead.Of(dissent).Any(c => c.outcome == CommitmentsRead.Outcomes.Broken), Is.False, "and nothing is broken.");
            var off = agreed.Clone();
            off.ledger.plans.Clear();
            Assert.That(CommitmentsRead.Of(off).Where(c => c.kind == CommitmentsRead.Kinds.Call).Select(c => c.title), Is.All.EqualTo("Your call in " + PactName),
                "Without a plan, the call it always was.");
        }

        [Test]
        public void GameSenseSaysAPlanBackedCallAsThePlan()
        {
            var open = PactPlanTests.Opened(out _);
            var npcs = NpcIds(open);
            var agreed = Answer(open, npcs[3], npcs[1]);
            Assert.That(GameSense.Evaluate(agreed).notes.Single(n => n.rowKind == "call").text,
                Is.EqualTo("Week " + agreed.week + ": you went with your alliance's plan; 2 went with it, 0 did not."));
            var pushed = agreed.Clone();
            pushed.ledger.plans[0].stance = PactPlanStance.Countered;
            pushed.ledger.plans[0].counterId = npcs[1];
            Assert.That(GameSense.Evaluate(pushed).notes.Single(n => n.rowKind == "call").text, Does.StartWith("Week " + agreed.week + ": your alliance went with your push; 2 went with it,"));
            var off = agreed.Clone();
            off.ledger.plans.Clear();
            Assert.That(GameSense.Evaluate(off).notes.Single(n => n.rowKind == "call").text,
                Is.EqualTo("Week " + agreed.week + ": you called the vote in your alliance; 2 followed, 0 did not."), "Without a plan, as it always read.");
        }

        // ------------------------------------------------------------ CampaignBrief

        [Test]
        public void TheCampaignOffersAPactOfThreeItsPlanNotACallTheEngineRefuses()
        {
            var s = PactPlanTests.WarRoom(out var pact);
            var npcs = NpcIds(s);
            var pair = PactPlanTests.Pact(s, "alliance-pair", "The Pair", s.playerId, npcs[3]);
            Assert.That(CampaignBrief.CanCallIn(s, pact), Is.False, "A pact of three settles its call when it meets,");
            Assert.That(CampaignBrief.CanCallIn(s, pair), Is.True, "and a pair calls as ever.");
            Assert.That(Goals(s), Is.EqualTo(new[] { "[ ] Call the vote in The Pair · Once this week", "[ ] Settle " + PactName + "'s plan · Meet first" }));
            Assert.That(CampaignBrief.PlanPoint(s, pact), Is.EqualTo("Meet " + PactName + " through one of its members to settle who the bloc evicts: once a week."));
            Assert.That(CampaignBrief.PlanPoint(s, pair), Is.Null, "A pair has no plan.");
            var refused = PactPlanTests.Apply(new EpisodeEngine(s), EpisodeCommandKind.CallTheVote, npcs[3], s.nominees[0], PactId);
            Assert.That(refused.reason, Is.EqualTo(PactPlans.CallRefusal(PactName)), "The goal it replaces is one the engine refuses.");

            var open = PactPlanTests.Opened(out pact);
            Assert.That(Goals(open).Single(g => g.StartsWith("[ ] Settle")), Is.EqualTo("[ ] Settle " + PactName + "'s plan · Waiting on you"));
            Assert.That(CampaignBrief.PlanPoint(open, pact), Is.EqualTo(PactName + "'s plan waits on your answer: tell somebody who was at the meeting."));
            var agreed = Answer(open, NpcIds(open)[3], NpcIds(open)[1]);
            Assert.That(Goals(agreed), Is.EqualTo(new[] { "[x] Settle " + PactName + "'s plan · Went with it" }), "Settled, and no call goal for the call it made.");
            Assert.That(CampaignBrief.PlanPoint(agreed, pact), Is.EqualTo("You have settled " + PactName + "'s plan this week."));
            Assert.That(CampaignBrief.PlanProgress(new PactPlanRow { stance = PactPlanStance.Countered }), Is.EqualTo("Pushed back"));
            Assert.That(CampaignBrief.PlanProgress(new PactPlanRow { stance = PactPlanStance.Low }), Is.EqualTo("Lay low"));

            var off = PactPlanTests.WarRoom(out pact, rules: false);
            Assert.That(Goals(off), Is.EqualTo(new[] { "[ ] Call the vote in " + PactName + " · Once this week" }), "Without the war rooms, the call it always was.");
            Assert.That(CampaignBrief.PlanPoint(off, pact), Is.Null);
        }

        // ------------------------------------------------------------ fixtures

        private static List<string> NpcIds(EpisodeState s) => s.Active.Where(c => !c.isPlayer).Select(c => c.id).ToList();

        private static string Name(EpisodeState s, string id) => s.Find(id).name;

        /// <summary>The campaign goals of the vote's own kinds - calls and plans - as their rows say them.</summary>
        private static string[] Goals(EpisodeState s) =>
            CampaignBrief.Goals(s).Where(g => g.kind == CampaignBrief.GoalKinds.Call || g.kind == CampaignBrief.GoalKinds.Plan).Select(g => g.ToString()).ToArray();

        /// <summary>The player answers the open plan through a member: a nominee to go with or push for, or nobody, to lie low.</summary>
        private static EpisodeState Answer(EpisodeState open, string through, string nominee)
        {
            var result = PactPlanTests.Apply(new EpisodeEngine(open), EpisodeCommandKind.AnswerPactPlan, through, nominee, PactId);
            Assert.That(result.accepted, Is.True, result.reason);
            return result.state;
        }

        /// <summary>The house plays to the reveal, the player voting out <paramref name="vote"/>.</summary>
        private static EpisodeState Reveal(EpisodeState before, string vote)
        {
            var engine = new EpisodeEngine(before);
            for (int i = 0; i < 40 && !engine.Snapshot.evictionResolved; i++)
            {
                var next = EpisodeEngineTests.NextCommand(engine.Snapshot);
                if (next.kind == EpisodeCommandKind.CastVote) next.targetId = vote;
                var result = engine.Apply(next);
                Assert.That(result.accepted, Is.True, result.reason);
            }
            Assert.That(engine.Snapshot.evictionResolved, Is.True, "The eviction resolved.");
            return engine.Snapshot;
        }
    }
}
