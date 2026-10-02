using System;
using System.Collections.Generic;
using System.Linq;
using Gamesim.Simulation;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// Allies share intel (ACTIONS-DEALS-ALLIANCES-PLAN C6), under the commitment rules of schema 22: an
    /// ally answers "where's your head at" with their ballot and never deflects, a member who has
    /// cooled, turned or betrayed answers as anybody does, and a stranger as they always did; an
    /// information partner who is an ally tells the truth in the deal's reading too; and a pact meets in
    /// any private room - every member still in the house at it, +3 for every pair, and in a vote week
    /// one member's ballot as an ally's claim, judged at the reveal - once a week. Each rule is shown as a
    /// season without the rules plays it, as every recorded season does, and as it plays under them.
    /// Unity-free, so the dotnet subset runs it (Tools/SimulationTests).
    /// </summary>
    public sealed class AlliesShareIntelTests
    {
        private const string PactId = "alliance-intel", PactName = "The Intel Pact";
        private const string OtherPactId = "alliance-other", OtherPactName = "The Other Pact";

        // ------------------------------------------------------------ "where's your head at?"

        [Test]
        public void C6_UnderTheRulesAnAllyAnswersWithTheirBallotAndNeverDeflects()
        {
            foreach (bool rules in new[] { false, true })
            {
                // A strategist, not Loyal, who is not close to the player: a stranger like them keeps it to themselves.
                var s = Campaign(rules);
                string ally = Voter(s, 0);
                s.Find(ally).traits = new List<string> { "Strategic", "Social" };
                SetScore(s, ally, s.playerId, 10);
                Pact(s, PactId, PactName, s.playerId, ally);
                Valid(s);
                string truth = EpisodeEngine.ProjectBallot(s, ally).selectedNomineeId;
                var engine = new EpisodeEngine(s);
                var result = Apply(engine, EpisodeCommandKind.AskVote, ally);
                Assert.That(result.accepted, Is.True, result.reason);
                var after = engine.Snapshot;
                var line = after.events.Last();
                Assert.That(line.kind, Is.EqualTo("vote-read"));
                Assert.That(after.randomState, Is.EqualTo(s.randomState), "Nothing is drawn: a deflection keeps nothing, and an ally's answer is decided.");
                Assert.That(EpisodeEngine.AskedThisWeek(after, ally), Is.True, "Either way it is the week's ask.");
                if (!rules)
                {
                    Assert.That(after.ledger.claims, Is.Empty, "Without the rules an ally kept it to themselves like anybody,");
                    Assert.That(line.text, Does.Contain("wouldn't say"));
                    continue;
                }
                var claim = after.ledger.claims.Single();
                Assert.That((claim.voterId, claim.source, claim.status), Is.EqualTo((ally, ClaimSource.Told, ClaimStatus.Open)),
                    "Under them they answer to the player's face, the week's ask,");
                Assert.That(claim.targetId, Is.EqualTo(truth), "with their ballot as it stands.");
                Assert.That(after.ledger.standings.Any(r => r.source == ClaimSource.Deflected), Is.False, "Never a deflection.");
                Assert.That(line.text, Is.EqualTo("You asked " + after.Find(ally).name + " where their head is at. \"" + after.Find(truth).name + ".\""),
                    "The line an answer always was.");
                Assert.That(VoteRead.Read(after).voters.Single(v => v.voterId == ally).saysId, Is.EqualTo(truth), "The whip count reads it.");
            }
        }

        [Test]
        public void C6_UnderTheRulesASneakyAllysWordIsTheTruth()
        {
            // Seasons in which a Sneaky voter who likes the player lies when asked without the rules: an
            // ally's answer under them is the truth on the same draw.
            int lies = 0;
            for (uint seed = 7; seed < 60 && lies < 3; seed++)
            {
                var off = Asked(false, seed, out string ally, out string truth);
                if (off.ledger.claims.Single().targetId == truth) continue;
                lies++;
                var on = Asked(true, seed, out ally, out truth);
                var claim = on.ledger.claims.Single();
                Assert.That(claim.targetId, Is.EqualTo(truth), "Seed " + seed + ": a Sneaky ally lied to a stranger's question and tells the player the truth.");
                Assert.That(claim.source, Is.EqualTo(ClaimSource.Told));
            }
            Assert.That(lies, Is.GreaterThan(0), "No season of the first fifty drew a lie from a Sneaky voter.");
        }

        [Test]
        public void C6_AMemberWhoHasCooledTurnedOrBetrayedAnswersAsAnybodyDoes()
        {
            // A strategist at a view of 10 holds; one at -20 has gone cold (under the quiet line); one at 10
            // who nominated the player has turned on the pact; a stranger at 10 is no ally at all.
            foreach (var (how, view, betrayed, pact, answers) in new[]
                     {
                         ("an ally whose pact holds", 10.0, false, true, true),
                         ("an ally gone cold", Allegiance.QuietLine - 10, false, true, false),
                         ("an ally who turned on the pact", 10.0, true, true, false),
                         ("a stranger", 10.0, false, false, false),
                     })
            {
                var s = Campaign(true);
                string member = Voter(s, 0);
                s.Find(member).traits = new List<string> { "Strategic", "Social" };
                SetScore(s, member, s.playerId, view);
                if (pact) Pact(s, PactId, PactName, s.playerId, member);
                if (betrayed) Betray(s, member, Allegiance.Nominated);
                Valid(s);
                var engine = new EpisodeEngine(s);
                Assert.That(Apply(engine, EpisodeCommandKind.AskVote, member).accepted, Is.True, how);
                var after = engine.Snapshot;
                Assert.That(after.ledger.claims.Any(), Is.EqualTo(answers), how + (answers ? " answers." : " keeps it to themselves, as a stranger like them does."));
                Assert.That(after.ledger.standings.Any(r => r.fromId == member && r.source == ClaimSource.Deflected), Is.EqualTo(!answers), how);
                Assert.That(EpisodeEngine.SharesIntel(s, member), Is.EqualTo(answers), how + ": the reader says the same.");
            }
        }

        [Test]
        public void C6_ABetrayalThePlayerCannotKnowChangesNothingInWhoAnswersAsAnAlly()
        {
            // A strategist at a view of 10, in a pact of three with the player, who turned on it last
            // week: by voting to evict the player, a ballot the player cannot place, or by nominating
            // them, which the player saw. Who answers as an ally follows what the player can know: a
            // hidden ballot betrayal must not be told by a deflection, or by silence at a meeting.
            foreach (bool known in new[] { false, true })
            {
                string label = known ? "A nomination the player saw" : "A vote against the player they cannot place";
                foreach (bool meeting in new[] { false, true })
                {
                    var s = Campaign(true);
                    string member = Voter(s, 0), other = Voter(s, 1);
                    s.Find(member).traits = new List<string> { "Strategic", "Social" };
                    Pact(s, PactId, PactName, s.playerId, member, other);
                    foreach (var id in new[] { s.playerId, member, other })
                        foreach (var to in new[] { s.playerId, member, other }.Where(t => t != id)) SetScore(s, id, to, 30);
                    SetScore(s, member, s.playerId, 10);
                    Betray(s, member, known ? Allegiance.Nominated : Allegiance.VotedAgainst);
                    s.week = 2;
                    Valid(s);
                    Assert.That(Allegiance.Betrayed(s, member), Is.True, label + ": the betrayal stands on the record,");
                    Assert.That(Allegiance.KnownBetrayed(s, member), Is.EqualTo(known), label + ": and the player " + (known ? "knows it." : "cannot know it."));
                    if (!meeting)
                    {
                        var engine = new EpisodeEngine(s);
                        Assert.That(Apply(engine, EpisodeCommandKind.AskVote, member).accepted, Is.True, label);
                        var asked = engine.Snapshot;
                        bool deflected = asked.ledger.standings.Any(r => r.fromId == member && r.source == ClaimSource.Deflected);
                        Assert.That(deflected, Is.EqualTo(known), label + (known ? ": they answer as anybody does, and keep it to themselves." : ": they answer as an ally,"));
                        if (known) continue;
                        var claim = asked.ledger.claims.Single(k => k.voterId == member);
                        Assert.That((claim.source, claim.targetId), Is.EqualTo((ClaimSource.Told, EpisodeEngine.ProjectBallot(s, member).selectedNomineeId)),
                            label + ": with their true ballot, to the player's face.");
                        continue;
                    }
                    var met = Meet(s, member);
                    Assert.That(met.ledger.claims.Single().voterId, Is.EqualTo(known ? other : member),
                        label + (known ? ": at a meeting they say nothing, and the other member does." : ": at a meeting they say where their vote is going."));
                }
            }
        }

        [Test]
        public void C6_AStrangersAnswerIsUnchangedByTheRules()
        {
            // The same question to the same stranger, with and without the rules: the same line, the same
            // record and the same draw - a deflection draws none, an answer draws one.
            foreach (bool strategist in new[] { true, false })
            {
                var outcomes = new List<string>();
                foreach (bool rules in new[] { false, true })
                {
                    var s = Campaign(rules);
                    string stranger = Voter(s, 0);
                    s.Find(stranger).traits = strategist ? new List<string> { "Strategic", "Social" } : new List<string> { "Social" };
                    SetScore(s, stranger, s.playerId, 10);
                    Valid(s);
                    var engine = new EpisodeEngine(s);
                    Assert.That(Apply(engine, EpisodeCommandKind.AskVote, stranger).accepted, Is.True);
                    var after = engine.Snapshot;
                    outcomes.Add(after.randomState + "|" + after.events.Last().text + "|"
                        + string.Join(";", after.ledger.claims.Select(k => k.voterId + ">" + k.targetId + ":" + k.source))
                        + "|" + string.Join(";", after.ledger.standings.Select(r => r.fromId + ":" + r.source)));
                    Assert.That(after.randomState == s.randomState, Is.EqualTo(strategist), strategist ? "A deflection draws nothing." : "An answer draws one.");
                }
                Assert.That(outcomes[1], Is.EqualTo(outcomes[0]), (strategist ? "A deflection" : "An answer") + " is the same with the rules as without them.");
            }
        }

        [Test]
        public void C6_ADeflectionTellsThePlayerWhereAnAllyStands()
        {
            // Under the rules an ally never deflects, so a member who does has told the player they have
            // gone cold: the notes say it, in words, and the vote read counts their pact terms (C3's rule).
            var s = Campaign(true);
            string member = Voter(s, 0);
            s.Find(member).traits = new List<string> { "Strategic", "Social" };
            SetScore(s, member, s.playerId, Allegiance.QuietLine - 10);
            Pact(s, PactId, PactName, s.playerId, member);
            Valid(s);
            Assert.That(Allegiance.CommitmentKnown(s, member), Is.False, "Precondition: nothing yet this week tells the player where they stand.");
            Assert.That(Quiet(s, member), Is.False);
            Assert.That(VoteRead.CommitmentHidden(s, member), Is.True);
            var engine = new EpisodeEngine(s);
            Assert.That(Apply(engine, EpisodeCommandKind.AskVote, member).accepted, Is.True);
            var after = engine.Snapshot;
            Assert.That(after.events.Last().text, Does.Contain("wouldn't say"), "They would not say,");
            Assert.That(Allegiance.CommitmentKnown(after, member), Is.True, "which tells the player where they stand:");
            var note = HouseguestNotes.For(after, member).Single(n => n.text.Contains("gone quiet"));
            Assert.That(note.text.Any(char.IsDigit), Is.False, "the notes say they have gone quiet, never by how much,");
            Assert.That(VoteRead.CommitmentHidden(after, member), Is.False, "and the read counts their pact terms, as the notes speak.");
        }

        [Test]
        public void C6_AnInformationPartnerWhoIsAnAllyTellsTheTruthInTheReading()
        {
            // A Sneaky partner who likes the player, on a season whose reading coin lands between a
            // quarter and nine in ten: as a partner alone their word lies on it (C1); as an ally too, the
            // reading is the truth. The coin is the deal's either way, so the season's stream is untouched.
            uint seed = SeasonWithAMiddlingReadingCoin();
            foreach (bool allied in new[] { false, true })
            {
                var s = Campaign(true, seed);
                string partner = Voter(s, 0), gone = s.nominees[0], stays = s.nominees[1];
                s.Find(partner).traits = new List<string> { "Sneaky" };
                SetScore(s, partner, gone, -100); SetScore(s, partner, stays, 100);
                SetScore(s, partner, s.playerId, 100);
                s.deals.Add(new DealState
                {
                    id = "deal-info", type = DealKind.InformationSharing, proposerId = s.playerId, recipientId = partner,
                    status = DealStatus.Active, week = s.week, expiresWeek = 0, trustImpact = DealKind.DefaultTrust(DealKind.InformationSharing),
                });
                if (allied) Pact(s, PactId, PactName, s.playerId, partner);
                Valid(s);
                uint stream = s.randomState;
                var engine = new EpisodeEngine(s);
                var closed = Apply(engine, EpisodeCommandKind.Advance);
                Assert.That(closed.accepted, Is.True, closed.reason);
                Assert.That(closed.state.randomState, Is.EqualTo(stream), "The reading is drawn on the deal's keyed coin.");
                var claim = closed.state.ledger.claims.Single(k => k.voterId == partner);
                Assert.That(claim.source, Is.EqualTo(ClaimSource.Told), "The deal's reading, told to the player's face (C1).");
                Assert.That(claim.targetId, Is.EqualTo(allied ? gone : stays),
                    allied ? "An ally's word is the truth, in the reading as when asked." : "A partner who is no ally keeps C1's odds: a Sneaky word lies on this coin.");
            }
        }

        // ------------------------------------------------------------ the meeting: where, who and how warm

        [Test]
        public void C6_APactMeetsInAnyPrivateRoom()
        {
            foreach (var room in new[] { "Bedroom", "Yard", "HoH", "Games" })
                Assert.That(EpisodeEngine.IsPrivateRoom(room), Is.True, room + " is a room where nobody listens.");
            // The open rooms, the ceremonies' room, and the private room, which is the diary room's.
            foreach (var room in new[] { "Living", "Kitchen", "Nomination", "Private", "", null, "the backyard" })
                Assert.That(EpisodeEngine.IsPrivateRoom(room), Is.False, (room ?? "null") + " is not.");
            Assert.That(RoomWords.Rooms.Count(EpisodeEngine.IsPrivateRoom), Is.EqualTo(4), "Four of the house's eight rooms.");
            // The engine never knows the room: the line says where nobody listens, never "the backyard".
            var s = FreeTimeWithAPactOfThree(true, out string first, out _);
            var after = Meet(s, first);
            Assert.That(Said(after).text, Does.Not.Contain("backyard"));
            Assert.That(Said(after).text, Does.Contain("where nobody listens"));
        }

        [Test]
        public void C6_UnderTheRulesAMeetingWarmsEveryPairAtItByThree()
        {
            foreach (bool rules in new[] { false, true })
            {
                var s = FreeTimeWithAPactOfThree(rules, out string first, out string second);
                var after = Meet(s, first);
                Assert.That(EpisodeEngine.SocialActionsSpent(after), Is.EqualTo(EpisodeEngine.SocialActionsSpent(s) + 1), "A meeting is a social action, as every room act is.");
                if (!rules)
                {
                    Assert.That(after.Score(after.playerId, first) - s.Score(s.playerId, first), Is.EqualTo(EpisodeEngine.AllianceMeetWarmth),
                        "Without the rules it is a word with the one ally,");
                    Assert.That(after.Score(after.playerId, second), Is.EqualTo(s.Score(s.playerId, second)), "and nobody else is at it,");
                    Assert.That(after.Score(first, second), Is.EqualTo(s.Score(first, second)));
                    Assert.That(Said(after).text, Is.EqualTo("You and " + after.Find(first).name + " went over the plan in the backyard, where nobody listens."));
                    continue;
                }
                foreach (var member in new[] { first, second })
                {
                    Assert.That(after.Score(after.playerId, member) - s.Score(s.playerId, member), Is.EqualTo(EpisodeEngine.AllianceMeetingWarmth),
                        "The player and each member at it: +3.");
                    double theirs = after.Score(member, after.playerId) - s.Score(member, s.playerId);
                    Assert.That(theirs, Is.GreaterThanOrEqualTo(EpisodeEngine.AllianceMeetingWarmth * 0.8).And.LessThan(EpisodeEngine.AllianceMeetingWarmth * 1.2),
                        "Their half is the engine's reciprocal of +3, as any conversation's is.");
                    Assert.That(after.memories.Count(m => m.ownerId == member && m.subjectId == after.playerId && m.text == EpisodeEngine.MeetingMemory(s.week)), Is.EqualTo(1),
                        "Each member remembers it.");
                }
                Assert.That(after.Score(first, second) - s.Score(first, second), Is.EqualTo(EpisodeEngine.AllianceMeetingWarmth), "Between the members, +3 each way,");
                Assert.That(after.Score(second, first) - s.Score(second, first), Is.EqualTo(EpisodeEngine.AllianceMeetingWarmth));
                foreach (var (from, to) in new[] { (first, second), (second, first) })
                {
                    var record = Entries(after, from, to, EpisodeEngine.PactMeetingType).Single();
                    Assert.That((record.impactScore, record.description), Is.EqualTo((EpisodeEngine.AllianceMeetingWarmth, PactName + " met in week " + s.week)),
                        "on the record as the house's own meetings write theirs,");
                    Assert.That(record.decayable, Is.True, "fading as theirs does,");
                    Assert.That(Entries(after, from, to, "alliance-meeting"), Is.Empty,
                        "under a type of its own: the house's private meetings are what the Spy Screen shows the player.");
                }
                // Arcs are the player's: one each from the player's own pair, none from the houseguests'.
                foreach (var member in new[] { first, second })
                    Assert.That(Arc(after, member) - Arc(s, member), Is.EqualTo(1), "One arc entry for " + member + ": the player's pair, never the members' own.");
                var line = Said(after);
                Assert.That(after.events.Count(e => e.kind == "conversation" && e.sequence >= s.nextSequence), Is.EqualTo(1), "One line for the meeting,");
                Assert.That(line.text, Is.EqualTo(PactName + " met where nobody listens: you, " + after.Find(first).name + " and " + after.Find(second).name + " went over the plan."));
                Assert.That(line.audienceIds, Is.EquivalentTo(new[] { after.playerId, first, second }), "Told to everybody at it.");
                Assert.That(line.text.Any(char.IsDigit), Is.False, "Never a number.");
                Assert.That(after.ledger.claims, Is.Empty, "Free time is no vote week: the warmth alone.");
            }
        }

        [Test]
        public void C6_EveryMemberStillInTheHouseIsAtTheMeetingAndNobodyGone()
        {
            var s = FreeTimeWithAPactOfThree(true, out string first, out string second);
            // A third member left the house with the last eviction: the pact's record keeps them (X5).
            string gone = Npcs(s).First(c => c.id != first && c.id != second).id;
            s.alliances.Single(a => a.id == PactId).members.Add(gone);
            s.Find(gone).status = ContestantStatus.Jury;
            Valid(s);
            var after = Meet(s, second);
            Assert.That(Said(after).audienceIds, Is.EquivalentTo(new[] { after.playerId, first, second }), "Every member still in the house is at it,");
            Assert.That(after.Score(after.playerId, first) - s.Score(s.playerId, first), Is.EqualTo(EpisodeEngine.AllianceMeetingWarmth),
                "the one it was not held through included,");
            Assert.That(after.Score(after.playerId, gone), Is.EqualTo(s.Score(s.playerId, gone)), "and nobody gone.");
            Assert.That(Entries(after, first, gone, EpisodeEngine.PactMeetingType), Is.Empty);
            Assert.That(EpisodeEngine.AtTheMeeting(s, s.alliances.Single(a => a.id == PactId)), Is.EqualTo(new[] { first, second }), "The pact's own order, the departed left out.");
        }

        // ------------------------------------------------------------ the meeting: the vote

        [Test]
        public void C6_InAVoteWeekAMeetingWritesOneAllyClaimFromAMembersBallot()
        {
            foreach (bool rules in new[] { false, true })
            {
                var s = CampaignWithAPactOfThree(rules, out string first, out string second);
                var after = Meet(s, first);
                if (!rules)
                {
                    Assert.That(after.ledger.claims, Is.Empty, "Without the rules a word in the backyard told nobody's vote.");
                    continue;
                }
                var claim = after.ledger.claims.Single();
                Assert.That((claim.voterId, claim.source, claim.status, claim.week), Is.EqualTo((first, ClaimSource.Ally, ClaimStatus.Open, s.week)),
                    "One claim, an ally's, from the one the meeting was held through: nobody's vote heard yet.");
                Assert.That(claim.targetId, Is.EqualTo(EpisodeEngine.ProjectBallot(after, first).selectedNomineeId), "Their ballot as it stands.");
                Assert.That(Said(after).text, Does.EndWith(" " + after.Find(first).name + " told the pact they're voting to evict " + after.Find(claim.targetId).name + "."),
                    "The line says what was said.");
                var read = VoteRead.Read(after).voters.Single(v => v.voterId == first);
                Assert.That((read.saysId, read.saysSource), Is.EqualTo((claim.targetId, ClaimSource.Ally)), "The whip count reads it as an ally's word.");
                Assert.That(KnownBallots.Read(after, s.week).Of(first), Is.Null, "Before the reveal it is a word, not a ballot.");
            }
        }

        [Test]
        public void C6_TheMeetingTellsTheVoteOfAnAllyThePlayerHasNotHeard()
        {
            // The first member's vote already asked this week: the second says theirs.
            var s = CampaignWithAPactOfThree(true, out string first, out string second);
            var engine = new EpisodeEngine(s);
            Assert.That(Apply(engine, EpisodeCommandKind.AskVote, first).accepted, Is.True);
            var asked = engine.Snapshot;
            var after = Meet(asked, first);
            var claim = after.ledger.claims.Single(k => k.source == ClaimSource.Ally);
            Assert.That(claim.voterId, Is.EqualTo(second), "The vote the player has not heard.");
            Assert.That(claim.targetId, Is.EqualTo(EpisodeEngine.ProjectBallot(after, second).selectedNomineeId));

            // Both heard: the one the meeting is held through, again.
            var both = CampaignWithAPactOfThree(true, out first, out second);
            var bothEngine = new EpisodeEngine(both);
            Assert.That(Apply(bothEngine, EpisodeCommandKind.AskVote, first).accepted, Is.True);
            Assert.That(Apply(bothEngine, EpisodeCommandKind.AskVote, second).accepted, Is.True);
            var heard = Meet(bothEngine.Snapshot, second);
            Assert.That(heard.ledger.claims.Single(k => k.source == ClaimSource.Ally).voterId, Is.EqualTo(second));
        }

        [Test]
        public void C6_AMemberWhoIsNoAllySaysNothingAtTheMeeting()
        {
            // The first has gone cold: the second, an ally, says theirs. Both cold: the warmth alone.
            var s = CampaignWithAPactOfThree(true, out string first, out string second);
            SetScore(s, first, s.playerId, Allegiance.QuietLine - 10);
            var after = Meet(s, first);
            Assert.That(after.ledger.claims.Single().voterId, Is.EqualTo(second), "A member gone cold says nothing; the ally does.");

            var cold = CampaignWithAPactOfThree(true, out first, out second);
            SetScore(cold, first, cold.playerId, Allegiance.QuietLine - 10);
            Betray(cold, second, Allegiance.Nominated);
            Valid(cold);
            var quiet = Meet(cold, first);
            Assert.That(quiet.ledger.claims, Is.Empty, "Nobody at it answers as an ally: no vote is told,");
            Assert.That(Said(quiet).text, Does.Not.Contain("voting"), "and the line says nothing of one.");
            Assert.That(quiet.Score(quiet.playerId, first) - cold.Score(cold.playerId, first), Is.EqualTo(EpisodeEngine.AllianceMeetingWarmth),
                "The meeting is still the warmth.");
            Assert.That(Allegiance.CommitmentKnown(quiet, first), Is.True, "And it was contact: the player can tell where each of them stands.");
        }

        [Test]
        public void C6_TheAllyClaimIsJudgedAtTheRevealLikeEveryClaim()
        {
            foreach (bool changesTheirMind in new[] { false, true })
            {
                string label = changesTheirMind ? "A vote that changed after the meeting" : "A vote as it was told";
                var s = CampaignWithAPactOfThree(true, out string first, out _);
                var met = Meet(s, first);
                var claim = met.ledger.claims.Single();
                string told = claim.targetId, other = met.nominees.First(id => id != told);
                if (changesTheirMind)
                {
                    // After the meeting the member comes to like the one they named and loathe the other.
                    SetScore(met, first, told, 100);
                    SetScore(met, first, other, -100);
                    Valid(met);
                    Assert.That(EpisodeEngine.ProjectBallot(met, first).selectedNomineeId, Is.EqualTo(other), "Precondition: their ballot now goes the other way.");
                }
                double mine = met.Score(met.playerId, first);
                var after = Reveal(met);
                var judged = after.ledger.claims.Single(k => k.source == ClaimSource.Ally);
                string cast = after.votes.Single(v => v.voterId == first).targetId;
                Assert.That(cast, Is.EqualTo(changesTheirMind ? other : told), label + ": the ballot.");
                Assert.That(judged.status, Is.EqualTo(changesTheirMind ? ClaimStatus.Lied : ClaimStatus.Kept), label + ": judged at the reveal.");
                var known = KnownBallots.Read(after, s.week).Of(first);
                Assert.That((known.targetId, known.basis, known.saidId, known.verdict), Is.EqualTo((cast, KnownBallots.Basis.Reported, told, judged.status)),
                    label + ": the player knows the ballot by an ally's account, as the existing source is read.");
                Assert.That(after.events.Any(e => e.kind == "vote-lie"), Is.False, label + ": an ally's account is no lie told to the player's face.");
                Assert.That(Entries(after, after.playerId, first, "vote-lie"), Is.Empty);
                Assert.That(after.Score(after.playerId, first), Is.EqualTo(mine), label + ": nothing costs them with the player for it.");

                // The pages say it as it was said: the member's own word to the pact, and a ballot that
                // went against it a vote that changed, never a lie.
                var note = HouseguestNotes.For(after, first).Single(n => n.kind == HouseguestNotes.Kinds.Vote);
                Assert.That(note.text, Is.EqualTo("Told the pact they'd vote out " + after.Find(told).name + (changesTheirMind ? " · voted the other way" : " · and did")),
                    label + ": the notes.");
                Assert.That(note.brief, Is.EqualTo(changesTheirMind ? "Voted the other way" : "Told the pact the truth about the vote"), label + ": the notes' brief.");
                var recap = YourWeek.Build(after, s.week).reads.Single(l => l.kind == YourWeek.Kinds.Claim && l.aboutId == first);
                Assert.That(recap.text, Does.StartWith(after.Find(first).name + " told the pact: evict " + after.Find(told).name), label + ": Your week.");
                foreach (var words in new[] { note.text, note.brief, recap.text })
                {
                    Assert.That(words, Does.Not.Contain("a lie").And.Not.Contain("Lied"), label + ": never a lie.");
                    Assert.That(words, Does.Not.Contain("An ally heard"), label + ": the member's own word, not an account of somebody else's.");
                }
            }
        }

        // ------------------------------------------------------------ the meeting: once a week

        [Test]
        public void C6_APactMeetsOnceAWeek()
        {
            var s = FreeTimeWithAPactOfThree(true, out string first, out string second);
            // A second pact with the first member.
            Pact(s, OtherPactId, OtherPactName, s.playerId, first);
            Valid(s);
            var met = Meet(s, first);
            var again = new EpisodeEngine(met).Apply(Command(met, EpisodeCommandKind.AllianceMeet, second, text: PactId));
            Assert.That(again.accepted, Is.False, "Once a week,");
            Assert.That(again.reason, Is.EqualTo(PactName + " has already met this week."), "and it says so.");
            Assert.That(EpisodeEngine.MeetingPact(s, first).id, Is.EqualTo(PactId), "The house offered the oldest pact that had not met;");
            var pact = met.alliances.Single(a => a.id == PactId);
            Assert.That(EpisodeEngine.MetThisWeek(met, pact), Is.True);
            Assert.That(EpisodeEngine.MeetingPact(met, second), Is.Null, "it offers no meeting of it again this week,");
            Assert.That(EpisodeEngine.MeetingPact(met, first).id, Is.EqualTo(OtherPactId), "while another pact may still meet.");
            var other = Meet(met, first, OtherPactId);
            Assert.That(Said(other).text, Does.StartWith(OtherPactName + " met"), "Each pact once a week.");
            // A command naming no pact holds the first that has not met; with none left it is refused.
            var none = new EpisodeEngine(other).Apply(Command(other, EpisodeCommandKind.AllianceMeet, first));
            Assert.That(none.accepted, Is.False);
            Assert.That(none.reason, Does.EndWith("has already met this week."));

            // The next week it meets again.
            var next = other.Clone();
            next.week++;
            Valid(next);
            Assert.That(EpisodeEngine.MetThisWeek(next, next.alliances.Single(a => a.id == PactId)), Is.False);
            Assert.That(Said(Meet(next, first, PactId)).text, Does.StartWith(PactName + " met"));
        }

        [Test]
        public void C6_AMeetingIsHeldThroughSomebodyInThePactItNames()
        {
            var s = FreeTimeWithAPactOfThree(true, out string first, out _);
            string outsider = Npcs(s).First(c => !s.alliances.Single(a => a.id == PactId).members.Contains(c.id)).id;
            Pact(s, OtherPactId, OtherPactName, s.playerId, outsider);
            Valid(s);
            var refused = new EpisodeEngine(s).Apply(Command(s, EpisodeCommandKind.AllianceMeet, first, text: OtherPactId));
            Assert.That(refused.accepted, Is.False);
            Assert.That(refused.reason, Is.EqualTo("Hold the meeting through somebody in that alliance."));
            var stranger = Npcs(s).First(c => !s.Allied(s.playerId, c.id)).id;
            var notAnAlly = new EpisodeEngine(s).Apply(Command(s, EpisodeCommandKind.AllianceMeet, stranger, text: PactId));
            Assert.That(notAnAlly.accepted, Is.False);
            Assert.That(notAnAlly.reason, Does.Contain("ally"), "A private meeting is for an ally, as it always was.");
        }

        // ------------------------------------------------------------ without the rules

        [Test]
        public void C6_WithoutTheRulesTheWordInTheBackyardIsAsItWas()
        {
            var s = FreeTimeWithAPactOfThree(false, out string first, out _);
            var once = Meet(s, first);
            var twice = Meet(once, first);
            Assert.That(Said(twice).text, Is.EqualTo("You and " + twice.Find(first).name + " went over the plan in the backyard, where nobody listens."),
                "Without the rules it can be had again the same week, as it always could,");
            Assert.That(twice.Score(twice.playerId, first) - s.Score(s.playerId, first), Is.EqualTo(2 * EpisodeEngine.AllianceMeetWarmth), "at +4 a time,");
            Assert.That(twice.story.cooldowns.Any(c => c.key.StartsWith("meeting:", StringComparison.Ordinal)), Is.False, "and nothing marks it.");
            Assert.That(EpisodeEngine.MeetingPact(s, first), Is.Null, "The house offers no pact meeting without the rules.");
            Assert.That(EpisodeEngine.SharesIntel(s, first), Is.False, "Nor does anybody answer as an ally.");
        }

        // ------------------------------------------------------------ fixtures

        private static List<ContestantState> Npcs(EpisodeState s) => s.Active.Where(c => !c.isPlayer).ToList();

        private static List<string> NpcIds(EpisodeState s) => Npcs(s).Select(c => c.id).ToList();

        /// <summary>The <paramref name="index"/>th houseguest who votes this week.</summary>
        private static string Voter(EpisodeState s, int index) => EpisodeEngine.Voters(s).Where(v => !v.isPlayer).Select(v => v.id).ElementAt(index);

        /// <summary>
        /// The campaign of the catalogue's six-house: a houseguest at the head of the house, the next two
        /// on the block, the fourth holding the veto; the player, the fourth and the fifth vote. The
        /// story's staging rules are on, as the director's seasons have them, so the room acts are played.
        /// </summary>
        private static EpisodeState Campaign(bool rules, uint seed = 7)
        {
            var s = ContentCatalog.Create(seed);
            s.strategyRulesStartWeek = 1;
            var npcs = NpcIds(s);
            s.phase = EpisodePhase.Campaign;
            s.hohId = npcs[0];
            s.nominees = new List<string> { npcs[1], npcs[2] };
            s.vetoHolderId = npcs[3];
            s.vetoPlayers = s.Active.Select(c => c.id).Take(EpisodeEngine.VetoPlayerCount(s.Active.Count())).ToList();
            if (!s.vetoPlayers.Contains(s.vetoHolderId)) s.vetoPlayers[s.vetoPlayers.Count - 1] = s.vetoHolderId;
            s.vetoResolved = true;
            EpisodeEngine.EnableStory(s);
            if (rules) EpisodeEngine.EnableCommitments(s);
            Valid(s);
            return s;
        }

        /// <summary>The campaign with the player in a pact with both houseguests who vote, each warm on the player and on each other.</summary>
        private static EpisodeState CampaignWithAPactOfThree(bool rules, out string first, out string second)
        {
            var s = Campaign(rules);
            first = Voter(s, 0);
            second = Voter(s, 1);
            Pact(s, PactId, PactName, s.playerId, first, second);
            foreach (var id in new[] { s.playerId, first, second })
                foreach (var to in new[] { s.playerId, first, second }.Where(t => t != id)) SetScore(s, id, to, 30);
            Valid(s);
            return s;
        }

        /// <summary>Free time in a story season of eight, the player in a pact of three, everybody in it at 30 with everybody else and the members at 0 with each other.</summary>
        private static EpisodeState FreeTimeWithAPactOfThree(bool rules, out string first, out string second)
        {
            var s = StorySeasonTests.StorySeason(43, 8);
            s.phase = EpisodePhase.Social;
            var npcs = NpcIds(s);
            string a = npcs[2], b = npcs[3];
            first = a;
            second = b;
            s.alliances.RemoveAll(pact => pact.members.Contains(a) || pact.members.Contains(b));
            Pact(s, PactId, PactName, s.playerId, first, second);
            foreach (var id in new[] { first, second })
            {
                SetScore(s, s.playerId, id, 30);
                SetScore(s, id, s.playerId, 30);
            }
            SetScore(s, first, second, 0);
            SetScore(s, second, first, 0);
            if (rules) EpisodeEngine.EnableCommitments(s);
            Valid(s);
            return s;
        }

        /// <summary>
        /// A season whose reading coin for the first voter of the campaign falls between 0.3 and 0.9: a
        /// partner whose word is worth a quarter lies on it, an ally's word is the truth on any.
        /// </summary>
        private static uint SeasonWithAMiddlingReadingCoin()
        {
            for (uint seed = 7; seed < 200; seed++)
            {
                var s = Campaign(true, seed);
                double coin = StoryRandom.Unit(s, EpisodeEngine.ReadingKey(s, Voter(s, 0)));
                if (coin > 0.3 && coin < 0.9) return seed;
            }
            Assert.Fail("No season of the first two hundred draws a middling coin for the reading.");
            return 0;
        }

        /// <summary>The campaign of <paramref name="seed"/>, its first voter a Sneaky ally of the player warm on them, asked where their head is at.</summary>
        private static EpisodeState Asked(bool rules, uint seed, out string ally, out string truth)
        {
            var s = Campaign(rules, seed);
            ally = Voter(s, 0);
            s.Find(ally).traits = new List<string> { "Sneaky" };
            SetScore(s, ally, s.playerId, 100);
            Pact(s, PactId, PactName, s.playerId, ally);
            Valid(s);
            truth = EpisodeEngine.ProjectBallot(s, ally).selectedNomineeId;
            var engine = new EpisodeEngine(s);
            var result = Apply(engine, EpisodeCommandKind.AskVote, ally);
            Assert.That(result.accepted, Is.True, result.reason);
            return engine.Snapshot;
        }

        /// <summary>A meeting held through <paramref name="through"/>, naming <paramref name="pactId"/> as the house's row does.</summary>
        private static EpisodeState Meet(EpisodeState s, string through, string pactId = PactId)
        {
            var engine = new EpisodeEngine(s);
            var result = engine.Apply(Command(s, EpisodeCommandKind.AllianceMeet, through, text: pactId));
            Assert.That(result.accepted, Is.True, result.reason);
            Valid(engine.Snapshot);
            return engine.Snapshot;
        }

        /// <summary>The meeting's line: the last conversation logged. The story's lore can follow a conversation with a line of its own.</summary>
        private static EpisodeEvent Said(EpisodeState s) => s.events.Last(e => e.kind == "conversation");

        /// <summary>The house plays to the reveal.</summary>
        private static EpisodeState Reveal(EpisodeState before)
        {
            var engine = new EpisodeEngine(before);
            for (int i = 0; i < 40 && !engine.Snapshot.evictionResolved; i++)
            {
                var result = engine.Apply(EpisodeEngineTests.NextCommand(engine.Snapshot));
                Assert.That(result.accepted, Is.True, result.reason);
            }
            Assert.That(engine.Snapshot.evictionResolved, Is.True, "The eviction resolved.");
            return engine.Snapshot;
        }

        private static void Valid(EpisodeState s) => Assert.That(EpisodeValidation.TryValidate(s, out var error), Is.True, error);

        private static void SetScore(EpisodeState s, string from, string to, double score)
        {
            var edge = s.relationships.FirstOrDefault(r => r.fromId == from && r.toId == to);
            if (edge == null) s.relationships.Add(edge = new RelationshipState { fromId = from, toId = to });
            edge.score = score;
        }

        /// <summary>A pact, with the ledger row every pact a season makes has: it began this week.</summary>
        private static AllianceState Pact(EpisodeState s, string id, string name, params string[] members)
        {
            var pact = new AllianceState { id = id, name = name, members = members.ToList(), active = true };
            s.alliances.Add(pact);
            s.ledger.alliances.Add(new AllianceRow { id = id, startedWeek = s.week, why = "player" });
            return pact;
        }

        /// <summary>An ally's betrayal on the player's record, as the engine writes it.</summary>
        private static void Betray(EpisodeState s, string npcId, string act) =>
            RelationshipLedger.RecordOneWay(s, s.playerId, npcId, Allegiance.BetrayedType, Allegiance.BetrayalImpact,
                Allegiance.Record(s.Find(npcId).name, act, s.alliances.Where(a => a.active && a.members.Contains(npcId) && a.members.Contains(s.playerId)).Select(a => a.name)));

        /// <summary>Whether the notes say this ally has gone quiet.</summary>
        private static bool Quiet(EpisodeState s, string id) =>
            HouseguestNotes.For(s, id).Any(n => n.kind == HouseguestNotes.Kinds.Pact && n.text.Contains("gone quiet"));

        /// <summary>How many entries the player's arc with this houseguest holds.</summary>
        private static int Arc(EpisodeState s, string npcId) =>
            s.relationshipArcs?.FirstOrDefault(a => a.npcId == npcId)?.weeklyHistory.Count ?? 0;

        private static List<RelationshipEventState> Entries(EpisodeState s, string from, string to, string type) =>
            s.relationships.Where(r => r.fromId == from && r.toId == to).SelectMany(r => r.events).Where(e => e.type == type).ToList();

        private static EpisodeCommand Command(EpisodeState s, EpisodeCommandKind kind, string target = null, string second = null, string text = null) =>
            new EpisodeCommand
            {
                id = "intel-" + kind + "-" + s.revision + "-" + target, actorId = s.playerId, kind = kind,
                targetId = target, secondTargetId = second, text = text, expectedRevision = s.revision, expectedPhase = s.phase,
            };

        private static CommandResult Apply(EpisodeEngine engine, EpisodeCommandKind kind, string target = null, string second = null, string text = null) =>
            engine.Apply(Command(engine.Snapshot, kind, target, second, text));
    }
}
