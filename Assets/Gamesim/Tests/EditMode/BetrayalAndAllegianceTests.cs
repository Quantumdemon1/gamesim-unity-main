using System;
using System.Collections.Generic;
using System.Linq;
using Gamesim.Simulation;
using Newtonsoft.Json;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// Betrayal as an event and two-sided alliances (ACTIONS-DEALS-ALLIANCES-PLAN C2 and C3, with X5),
    /// under the commitment rules of schema 22. Each rule is shown as a season without the rules plays
    /// it - as every recorded season does - and as it plays under them. Unity-free, so the dotnet subset
    /// runs it (Tools/SimulationTests).
    /// </summary>
    public sealed class BetrayalAndAllegianceTests
    {
        private const string PactId = "alliance-test", PactName = "The Test Pact";

        // ------------------------------------------------------------ C2: the triggers

        [Test]
        public void C2_AnAllyWhoNominatesThePlayerFlagsThePact()
        {
            foreach (bool rules in new[] { false, true })
            {
                var before = NominationsByAnAlly(rules, out string hoh);
                var after = Advance(before);
                Assert.That(after.nominees, Does.Contain(after.playerId), "Precondition: the ally put the player up.");
                var entries = Entries(after, after.playerId, hoh, Allegiance.BetrayedType);
                var lines = Added(before, after).Where(e => e.kind == Allegiance.LogKind).ToList();
                if (!rules)
                {
                    Assert.That(entries, Is.Empty, "A season without the rules writes nothing.");
                    Assert.That(lines, Is.Empty);
                    Assert.That(Allegiance.Holds(after, hoh, after.playerId), Is.True, "and the pact's shield holds, as it always did.");
                    Assert.That(Allegiance.FreeExit(after, hoh), Is.False);
                    continue;
                }
                string record = after.Find(hoh).name + " nominated you despite " + PactName + ".";
                Assert.That(entries, Has.Count.EqualTo(1));
                Assert.That(entries[0].description, Is.EqualTo(record));
                Assert.That(entries[0].impactScore, Is.EqualTo(Allegiance.BetrayalImpact));
                Assert.That(entries[0].decayable, Is.False, "The ledger's permanent alliance-betrayed entry.");
                Assert.That(Entries(after, hoh, after.playerId, Allegiance.BetrayedType), Is.Empty, "Held by the one betrayed.");
                Assert.That(lines, Has.Count.EqualTo(1));
                Assert.That(lines[0].text, Is.EqualTo(record + " You can cut ties with " + First(after, hoh) + " this week at no cost, or stay allied."));
                Assert.That(lines[0].audienceIds, Is.EquivalentTo(new[] { after.playerId, hoh }), "A nomination is public: the player is told.");
                Assert.That(lines[0].text.Any(char.IsDigit), Is.False, "No number reaches the player.");
                Assert.That(after.alliances.Single(a => a.id == PactId).active, Is.True, "The pact stands until the player decides.");
                Assert.That(Allegiance.Holds(after, hoh, after.playerId), Is.False, "Their protection has ended.");
                Assert.That(Allegiance.FreeExit(after, hoh), Is.True);
                Assert.That(after.Score(after.playerId, hoh), Is.EqualTo(before.Score(before.playerId, hoh)),
                    "Nobody's view moves: keeping the pact is the player's to choose.");
                Valid(after);

                // The way out is said where a leave is said: in free time, the campaign or a window, never at the nomination table.
                var engine = new EpisodeEngine(after);
                var early = Apply(engine, EpisodeCommandKind.LeaveAlliance, hoh);
                Assert.That(early.accepted, Is.False, "Cutting ties waits for a word with them, as leaving does.");
                Assert.That(early.reason, Is.EqualTo("Social actions are available during free time and campaigning."));
                Assert.That(engine.Snapshot.alliances.Single(a => a.id == PactId).active, Is.True);
                Assert.That(Allegiance.FreeExit(engine.Snapshot, hoh), Is.True, "and the week's way out is still open.");
            }
        }

        [Test]
        public void C2_AnAllyWhoNamesThePlayerTheReplacementFlagsThePact()
        {
            foreach (bool rules in new[] { false, true })
            {
                var before = VetoMeetingWithTheHeadAsAnAlly(rules, out string hoh);
                var after = Advance(before);
                Assert.That(after.nominees, Does.Contain(after.playerId), "Precondition: the veto was used and the player named in the saved nominee's place.");
                var entries = Entries(after, after.playerId, hoh, Allegiance.BetrayedType);
                if (!rules) { Assert.That(entries, Is.Empty); continue; }
                Assert.That(entries.Single().description, Is.EqualTo(after.Find(hoh).name + " named you as the replacement nominee despite " + PactName + "."));
                Assert.That(Added(before, after).Single(e => e.kind == Allegiance.LogKind).audienceIds, Is.EquivalentTo(new[] { after.playerId, hoh }));
                Assert.That(Allegiance.FreeExit(after, hoh), Is.True);
            }
        }

        /// <summary>
        /// A refused call is no betrayal on its own: nothing on the record, no line, no way out, and the
        /// member's terms still hold. It is the ballot that keeps or breaks it - one cast the other way is
        /// a betrayal at the reveal, told by that ballot, so it is the betrayer's alone until the player
        /// can place it.
        /// </summary>
        [Test]
        public void C2_AnAllyWhoIgnoresTheCallAndVotesTheOtherWayFlagsThePactAtTheReveal()
        {
            foreach (bool rules in new[] { false, true })
            {
                // Above the line their commitment lapses at, and under the loyalty that follows a call:
                // they like the target.
                var before = CallInAPactOfThree(rules, looseView: Allegiance.QuietLine + 5, out string loyal, out string loose, out string target, looseSparesTarget: true);
                var engine = new EpisodeEngine(before);
                var called = Apply(engine, EpisodeCommandKind.CallTheVote, loyal, target, PactId);
                Assert.That(called.accepted, Is.True, called.reason);
                var atTheCall = engine.Snapshot;
                Assert.That(atTheCall.ledger.calls.Single().defected, Is.EqualTo(new[] { loose }), "Precondition: one of them refused the call,");
                Assert.That(atTheCall.randomState, Is.EqualTo(before.randomState), "on loyalty alone.");
                Assert.That(Entries(atTheCall, atTheCall.playerId, loose, Allegiance.BetrayedType), Is.Empty, "A refusal alone turns on nothing:");
                Assert.That(Added(before, atTheCall).Where(e => e.kind == Allegiance.LogKind), Is.Empty, "no line,");
                Assert.That(Allegiance.FreeExit(atTheCall, loose), Is.False, "no way out,");
                Assert.That(Allegiance.Holds(atTheCall, loose, atTheCall.playerId), Is.True, "and their terms still hold.");

                var after = Reveal(atTheCall);
                Assert.That(after.votes.Single(v => v.voterId == loose).targetId, Is.Not.EqualTo(target), "Precondition: their ballot spared the target.");
                var entries = Entries(after, after.playerId, loose, Allegiance.BetrayedType);
                if (!rules) { Assert.That(entries, Is.Empty); continue; }
                string record = after.Find(loose).name + " ignored your call to evict " + after.Find(target).name + " despite " + PactName + ".";
                Assert.That(entries.Single().description, Is.EqualTo(record));
                Assert.That(Entries(after, after.playerId, loyal, Allegiance.BetrayedType), Is.Empty, "Whoever followed turned on nothing.");
                Assert.That(KnownBallots.Knows(after, after.week, loose), Is.False, "Precondition: the count proves nothing of it.");
                var line = Added(atTheCall, after).Single(e => e.kind == Allegiance.LogKind);
                Assert.That(line.text, Is.EqualTo(record));
                Assert.That(line.audienceIds, Is.EqualTo(new[] { loose }), "Told by a ballot: to the betrayer alone.");
                Assert.That(Allegiance.TellsABallot(after, loose, record), Is.True);
                Assert.That(HouseguestNotes.For(after, loose).Any(n => n.text.Contains("despite " + PactName)), Is.False, "The notes do not say it,");
                Assert.That(Allegiance.FreeExit(after, loose), Is.False, "and nothing offers a way out of what the player cannot know.");
                Assert.That(Allegiance.Holds(after, loose, after.playerId), Is.False, "The engine holds it all the same.");

                // Caught out by what they told the player: now the player knows.
                after.ledger.claims.Add(new ClaimRow { week = after.week, voterId = loose, targetId = target, source = ClaimSource.Told, status = ClaimStatus.Lied });
                Assert.That(KnownBallots.Knows(after, after.week, loose), Is.True);
                Assert.That(HouseguestNotes.For(after, loose).Single(n => n.text.Contains("despite " + PactName)).text,
                    Is.EqualTo(record.TrimEnd('.') + " · you can cut ties this week at no cost"));
                Assert.That(Allegiance.FreeExit(after, loose), Is.True, "Once the player knows, the way out opens - this week.");

                // A refusal whose ballot went the call's way all the same turns on nothing.
                var grudging = CallInAPactOfThree(true, looseView: Allegiance.QuietLine + 5, out loyal, out loose, out target, looseVotesTargetAnyway: true);
                var refusing = new EpisodeEngine(grudging);
                Assert.That(Apply(refusing, EpisodeCommandKind.CallTheVote, loyal, target, PactId).accepted, Is.True);
                Assert.That(refusing.Snapshot.ledger.calls.Single().defected, Is.EqualTo(new[] { loose }), "Precondition: they refused the call,");
                var kept = Reveal(refusing.Snapshot);
                Assert.That(kept.votes.Single(v => v.voterId == loose).targetId, Is.EqualTo(target), "and voted the target out anyway.");
                Assert.That(Entries(kept, kept.playerId, loose, Allegiance.BetrayedType), Is.Empty, "Their ballot kept the call: nothing to flag.");
                Assert.That(Added(grudging, kept).Where(e => e.kind == Allegiance.LogKind), Is.Empty);
            }
        }

        [Test]
        public void C2_AnAllyWhoBreaksAVetoCommitmentWithThePlayerFlagsThePact()
        {
            foreach (bool rules in new[] { false, true })
            {
                var before = VetoMeetingWithAnAllyHolding(rules, out string holder);
                var after = Advance(before);
                Assert.That(after.deals.Single(d => d.id == "deal-veto").status, Is.EqualTo(DealStatus.Broken), "Precondition: the veto was not used on the player.");
                var entries = Entries(after, after.playerId, holder, Allegiance.BetrayedType);
                if (!rules) { Assert.That(entries, Is.Empty); continue; }
                Assert.That(entries.Single().description, Is.EqualTo(after.Find(holder).name + " broke a veto commitment with you despite " + PactName + "."));
                Assert.That(Added(before, after).Single(e => e.kind == Allegiance.LogKind).audienceIds, Is.EquivalentTo(new[] { after.playerId, holder }),
                    "The veto meeting is the house's to see.");
                Assert.That(Allegiance.FreeExit(after, holder), Is.True);
            }
        }

        /// <summary>
        /// The ballot is the ally's own act, so the engine flags the pact on it - but the player cannot
        /// see a ballot the count does not prove and nobody told them. The line goes to the betrayer
        /// alone, the record is kept off the player's pages, and the free exit stays shut until the
        /// player knows. One act is one betrayal: the vote deal the same ballot broke adds nothing.
        /// </summary>
        [Test]
        public void C2_AnAllyWhoVotesAgainstThePlayerFlagsThePactOnTheBallotThePlayerCannotSee()
        {
            foreach (bool rules in new[] { false, true })
            {
                var before = CampaignWithThePlayerUp(rules, out string ally, out string other);
                var after = Reveal(before);
                Assert.That(after.Find(after.playerId).status, Is.EqualTo(ContestantStatus.Active), "Precondition: the player survives the vote.");
                Assert.That(after.votes.Single(v => v.voterId == ally).targetId, Is.EqualTo(after.playerId), "Precondition: the ally voted them out.");
                Assert.That(after.deals.Single(d => d.id == "deal-vote").status, Is.EqualTo(DealStatus.Broken), "and broke their vote deal by it.");
                var entries = Entries(after, after.playerId, ally, Allegiance.BetrayedType);
                if (!rules) { Assert.That(entries, Is.Empty); continue; }
                Assert.That(entries, Has.Count.EqualTo(1), "One act, one betrayal a week.");
                Assert.That(entries[0].description, Is.EqualTo(after.Find(ally).name + " voted to evict you despite " + PactName + "."));
                var line = Added(before, after).Single(e => e.kind == Allegiance.LogKind);
                Assert.That(line.audienceIds, Is.EqualTo(new[] { ally }), "Told by a ballot: to the betrayer alone.");
                Assert.That(KnownBallots.Knows(after, after.week, ally), Is.False, "Precondition: the count proves nothing of it.");
                Assert.That(KnownBallots.TellsAnUnknownBallot(after, ally, entries[0].description, entries[0].week), Is.True,
                    "Every page that prints the player's record leaves it out.");
                Assert.That(HouseguestNotes.For(after, ally).Any(n => n.text.Contains("voted to evict you")), Is.False, "The notes do not say it.");
                Assert.That(Allegiance.FreeExit(after, ally), Is.False, "and nothing offers a way out of what the player cannot know.");
                Assert.That(Allegiance.Holds(after, ally, after.playerId), Is.False, "The engine holds it all the same: their protection has ended.");

                // Told to their face, and caught out at the reveal: now the player knows.
                after.ledger.claims.Add(new ClaimRow { week = after.week, voterId = ally, targetId = other, source = ClaimSource.Told, status = ClaimStatus.Lied });
                Assert.That(KnownBallots.Knows(after, after.week, ally), Is.True);
                var note = HouseguestNotes.For(after, ally).Single(n => n.text.Contains("voted to evict you"));
                Assert.That(note.text, Is.EqualTo(after.Find(ally).name + " voted to evict you despite " + PactName + " · you can cut ties this week at no cost"));
                Assert.That(note.text.Any(char.IsDigit), Is.False);
                Assert.That(Allegiance.FreeExit(after, ally), Is.True, "Once the player knows, the way out opens - this week.");
            }
        }

        /// <summary>
        /// A ballot the reveal itself places - a claim it judges a lie - is the player's to know as the
        /// betrayal is written, so the line is theirs at once, with the way out it opens.
        /// </summary>
        [Test]
        public void C2_ABallotBetrayalTheRevealAlreadyPlacesIsToldWithTheWayOut()
        {
            var before = CampaignWithThePlayerUp(true, out string ally, out string other);
            // The ally told the player they would vote the other nominee out.
            before.ledger.claims.Add(new ClaimRow { week = before.week, voterId = ally, targetId = other, source = ClaimSource.Told, status = ClaimStatus.Open });
            Valid(before);
            var after = Reveal(before);
            Assert.That(after.votes.Single(v => v.voterId == ally).targetId, Is.EqualTo(after.playerId), "Precondition: they voted the player out,");
            Assert.That(KnownBallots.Knows(after, after.week, ally), Is.True, "and the reveal judged their word a lie, which places the ballot.");
            string record = after.Find(ally).name + " voted to evict you despite " + PactName + ".";
            var line = Added(before, after).Single(e => e.kind == Allegiance.LogKind);
            Assert.That(line.text, Is.EqualTo(record + " You can cut ties with " + First(after, ally) + " this week at no cost, or stay allied."));
            Assert.That(line.audienceIds, Is.EquivalentTo(new[] { after.playerId, ally }), "Told to the player, with the choice it opens.");
            Assert.That(Allegiance.FreeExit(after, ally), Is.True);
        }

        /// <summary>
        /// A vote deal an ally broke by their ballot is a betrayal the ballot tells, though the ballot was
        /// not cast against the player: kept off the player's pages, and its way out shut, until they can
        /// place it.
        /// </summary>
        [Test]
        public void C2_AnAllyWhoBreaksAVoteDealByABallotNotCastAgainstThePlayerFlagsThePactPrivately()
        {
            foreach (bool rules in new[] { false, true })
            {
                var before = CampaignWithAVoteDeal(rules, out string ally, out string target, out string spared);
                var after = Reveal(before);
                Assert.That(after.Find(after.playerId).status, Is.EqualTo(ContestantStatus.Active));
                Assert.That(after.votes.Single(v => v.voterId == ally).targetId, Is.EqualTo(spared), "Precondition: the ally voted the other nominee out,");
                Assert.That(after.deals.Single(d => d.id == "deal-evict").status, Is.EqualTo(DealStatus.Broken), "and broke their vote deal by it.");
                var entries = Entries(after, after.playerId, ally, Allegiance.BetrayedType);
                if (!rules) { Assert.That(entries, Is.Empty); continue; }
                string record = after.Find(ally).name + " broke a vote to evict with you despite " + PactName + ".";
                Assert.That(entries.Single().description, Is.EqualTo(record));
                Assert.That(Allegiance.TellsABallot(after, ally, record), Is.True, "A line a ballot tells.");
                Assert.That(KnownBallots.Knows(after, after.week, ally), Is.False, "Precondition: the count proves nothing of it,");
                Assert.That(KnownBallots.TellsAnUnknownBallot(after, ally, record, after.week), Is.True, "so every page that prints the record leaves it out:");
                Assert.That(Added(before, after).Single(e => e.kind == Allegiance.LogKind).audienceIds, Is.EqualTo(new[] { ally }), "the line,");
                Assert.That(HouseguestNotes.For(after, ally).Any(n => n.text.Contains("despite " + PactName)), Is.False, "the notes,");
                Assert.That(Allegiance.KnownBetrayals(after, ally), Is.Empty);
                Assert.That(Allegiance.FreeExit(after, ally), Is.False, "and the way out.");

                // Caught out by what they told the player: now the player knows.
                after.ledger.claims.Add(new ClaimRow { week = after.week, voterId = ally, targetId = target, source = ClaimSource.Told, status = ClaimStatus.Lied });
                Assert.That(KnownBallots.TellsAnUnknownBallot(after, ally, record, after.week), Is.False);
                Assert.That(HouseguestNotes.For(after, ally).Single(n => n.text.Contains("despite " + PactName)).text,
                    Is.EqualTo(record.TrimEnd('.') + " · you can cut ties this week at no cost"));
                Assert.That(Allegiance.FreeExit(after, ally), Is.True);
            }
        }

        /// <summary>
        /// Nothing is flagged between two who share no standing pact, nor once the player has left the
        /// house - they leave every pact with it (X5). Each beside the same act flagged, as the control.
        /// </summary>
        [Test]
        public void C2_NothingIsFlaggedWithoutASharedPactOrForAPlayerTheHouseEvicts()
        {
            var allied = Advance(NominationsByAnAlly(true, out string hoh));
            Assert.That(Entries(allied, allied.playerId, hoh, Allegiance.BetrayedType), Has.Count.EqualTo(1), "Control: an ally's nomination turns on the pact.");
            var unallied = NominationsByAnAlly(true, out hoh, pact: false);
            var nominated = Advance(unallied);
            Assert.That(nominated.nominees, Does.Contain(nominated.playerId), "Precondition: the same Head of Household put the player up,");
            Assert.That(Entries(nominated, nominated.playerId, hoh, Allegiance.BetrayedType), Is.Empty, "with no pact to turn on.");
            Assert.That(Added(unallied, nominated).Where(e => e.kind == Allegiance.LogKind), Is.Empty);
            Assert.That(Allegiance.FreeExit(nominated, hoh), Is.False);

            var kept = Reveal(CampaignWithThePlayerUp(true, out string ally, out _));
            Assert.That(Entries(kept, kept.playerId, ally, Allegiance.BetrayedType), Has.Count.EqualTo(1), "Control: a ballot against a player the house keeps turns on the pact.");
            var up = CampaignWithThePlayerUp(true, out ally, out _, houseKeeps: false);
            var gone = Reveal(up);
            Assert.That(gone.Find(gone.playerId).status, Is.Not.EqualTo(ContestantStatus.Active), "Precondition: the house voted the player out,");
            Assert.That(gone.votes.Single(v => v.voterId == ally).targetId, Is.EqualTo(gone.playerId), "their ally with it.");
            Assert.That(Entries(gone, gone.playerId, ally, Allegiance.BetrayedType), Is.Empty, "They left every pact with the house: nothing to turn on.");
            Assert.That(Added(up, gone).Where(e => e.kind == Allegiance.LogKind), Is.Empty);
        }

        // ------------------------------------------------------------ C2: what it changes

        /// <summary>
        /// The betrayer's protection terms stop counting: the shield, the veto pull, the vote, the jury
        /// and following a call. A pact the two begin in a later week is a fresh commitment.
        /// </summary>
        [Test]
        public void C2_TheBetrayersProtectionTermsStopCounting()
        {
            foreach (bool rules in new[] { false, true })
            {
                var s = Protected(rules, out string ally, out string other);
                Assert.That(Allegiance.Holds(s, ally, s.playerId), Is.True, "Precondition: a committed ally.");
                double shield = StrategyRules.NominationReluctance(s, ally, s.playerId);
                double pull = StrategyRules.VetoWillingness(s, ally, s.playerId);
                double vote = AllianceFactor(s, ally, s.playerId);
                Assert.That(shield, Is.EqualTo(s.Score(ally, s.playerId) + StrategyRules.AllyShield));
                Assert.That(pull, Is.EqualTo(s.Score(ally, s.playerId) + StrategyRules.VetoAllyPull));
                Assert.That(vote, Is.GreaterThan(0), "Precondition: their ballot weighs the pact toward keeping the player.");

                Betray(s, ally, Allegiance.Nominated);
                if (!rules)
                {
                    Assert.That(StrategyRules.NominationReluctance(s, ally, s.playerId), Is.EqualTo(shield), "Without the rules nothing reads the record.");
                    Assert.That(AllianceFactor(s, ally, s.playerId), Is.EqualTo(vote));
                    continue;
                }
                Assert.That(Allegiance.Holds(s, ally, s.playerId), Is.False);
                Assert.That(StrategyRules.NominationReluctance(s, ally, s.playerId), Is.EqualTo(shield - StrategyRules.AllyShield), "The shield.");
                Assert.That(StrategyRules.VetoWillingness(s, ally, s.playerId), Is.EqualTo(pull - StrategyRules.VetoAllyPull), "The veto pull.");
                Assert.That(AllianceFactor(s, ally, s.playerId), Is.Zero, "The vote.");
                Assert.That(WebVotingBlocs.FromNative(s).alliances.Single(a => a.id == PactId).members, Does.Not.Contain(ally), "The bloc they no longer answer to.");
                var levered = s.Clone();
                EpisodeEngine.EnableLevers(levered);
                var calling = new EpisodeEngine(levered);
                Assert.That(Apply(calling, EpisodeCommandKind.CallTheVote, ally, other, PactId).accepted, Is.True);
                Assert.That(calling.Snapshot.ledger.calls.Single().defected, Is.EqualTo(new[] { ally }), "Following a call:");
                Assert.That(calling.Snapshot.randomState, Is.EqualTo(levered.randomState), "they do not, and nothing is left to a roll.");
                Assert.That(StrategyRules.NominationReluctance(s, s.playerId, ally), Is.EqualTo(s.Score(s.playerId, ally) + StrategyRules.AllyShield),
                    "The player's own terms are the player's.");
                Assert.That(StrategyRules.NominationReluctance(s, other, ally), Is.EqualTo(s.Score(other, ally)), "Nobody else's terms move.");

                var jury = s.Clone();
                jury.Find(ally).status = ContestantStatus.Jury;
                Assert.That(WebJuryVoting.AllianceLoyalty(jury, ally, jury.playerId), Is.Zero, "The jury: a juror who turned on the player is no former ally of theirs.");

                // A pact begun in a later week is a fresh commitment.
                s.week = 3;
                s.alliances.Single(a => a.id == PactId).active = false;
                Pact(s, "alliance-again", "The Second Pact", s.playerId, ally);
                Assert.That(Allegiance.Holds(s, ally, s.playerId), Is.True, "A new pact, a new word.");
                Assert.That(StrategyRules.NominationReluctance(s, ally, s.playerId), Is.EqualTo(shield));
            }
        }

        /// <summary>
        /// A fresh pact restores the betrayer's word in that pact, never in the one they turned on: its
        /// loyalty and its bloc still count them out while it stands beside the new one.
        /// </summary>
        [Test]
        public void C2_AFreshPactDoesNotRestoreTheBetrayersTermsInThePactTheyTurnedOn()
        {
            var s = Protected(true, out string ally, out string third, pactOfThree: true);
            Betray(s, ally, Allegiance.Nominated);
            var betrayed = s.alliances.Single(a => a.id == PactId);
            Assert.That(Allegiance.LapsedMembers(s, betrayed), Is.EqualTo(new[] { ally }), "Precondition: they count for nothing in the pact they turned on.");

            s.week = 3;
            var fresh = Pact(s, "alliance-fresh", "The Fresh Pact", s.playerId, ally);
            Assert.That(Allegiance.Betrayed(s, ally), Is.False, "A new pact, a new word:");
            Assert.That(Allegiance.Holds(s, ally, s.playerId), Is.True, "their terms toward the player hold again,");
            Assert.That(Allegiance.LapsedMembers(s, fresh), Is.Empty, "in the fresh pact,");
            Assert.That(Allegiance.LapsedMembers(s, betrayed), Is.EqualTo(new[] { ally }), "and not in the one they turned on:");
            var blocs = WebVotingBlocs.FromNative(s).alliances;
            Assert.That(blocs.Single(a => a.id == PactId).members, Does.Not.Contain(ally), "its bloc,");
            Assert.That(blocs.Single(a => a.id == fresh.id).members, Does.Contain(ally));

            // Their ballot weighs the fresh pact's loyalty to the player, and nothing of the betrayed one's.
            var onlyFresh = s.Clone();
            onlyFresh.alliances.RemoveAll(a => a.id == PactId);
            onlyFresh.relationships.Single(r => r.fromId == onlyFresh.playerId && r.toId == ally).events.RemoveAll(e => e.type == Allegiance.BetrayedType);
            Assert.That(AllianceFactor(s, ally, s.playerId), Is.EqualTo(AllianceFactor(onlyFresh, ally, s.playerId)), "its loyalty.");
            Assert.That(AllianceFactor(s, third, s.playerId), Is.GreaterThan(AllianceFactor(onlyFresh, third, s.playerId)), "The loyal member's still counts.");
        }

        // ------------------------------------------------------------ C2: the player's choice

        /// <summary>
        /// Cutting ties the week of the betrayal costs nothing and nobody holds it against the player. A
        /// pact of three or more goes on without the betrayer: the player and the loyal members keep it.
        /// </summary>
        [Test]
        public void C2_CuttingTiesTheWeekOfTheBetrayalCostsNothingAndCutsTheBetrayerOutOfAPactOfThree()
        {
            var s = PactOfThreeWithABetrayal(rules: true, out string betrayer, out string loyal);
            int spent = EpisodeEngine.SocialActionsSpent(s);
            uint stream = s.randomState;
            double theirs = s.Score(betrayer, s.playerId);
            Assert.That(Allegiance.FreeExit(s, betrayer), Is.True);
            Assert.That(Allegiance.FreeExitKeepsAPact(s, betrayer), Is.True, "A pact of three goes on without them.");
            var engine = new EpisodeEngine(s);
            var left = Apply(engine, EpisodeCommandKind.LeaveAlliance, betrayer);
            Assert.That(left.accepted, Is.True, left.reason);
            var after = engine.Snapshot;
            var pact = after.alliances.Single(a => a.id == PactId);
            Assert.That(pact.active, Is.True, "The pact goes on,");
            Assert.That(pact.members, Is.EquivalentTo(new[] { after.playerId, loyal }), "the player and the loyal member in it, the betrayer cut out.");
            Assert.That(after.Allied(after.playerId, betrayer), Is.False);
            Assert.That(Allegiance.Holds(after, loyal, after.playerId), Is.True, "The loyal member's word is untouched.");
            Assert.That(Grudges.Severity(after, betrayer, after.playerId), Is.Zero, "No grudge from the betrayer,");
            Assert.That(Grudges.Severity(after, loyal, after.playerId), Is.Zero, "and none from the others: no 80 grudge on a betrayal exit.");
            Assert.That(after.Score(betrayer, after.playerId), Is.EqualTo(theirs), "No warmth lost with them,");
            Assert.That(after.randomState, Is.EqualTo(stream), "so no roll drawn,");
            Assert.That(EpisodeEngine.SocialActionsSpent(after), Is.EqualTo(spent), "and no action spent.");
            Assert.That(after.ledger.alliances.Single(r => r.id == PactId).endedWeek, Is.Zero, "The pact's row stays open.");
            var line = after.events.Last();
            Assert.That(line.text, Is.EqualTo("You cut ties with " + after.Find(betrayer).name + " after they turned on " + PactName + ". "
                + First(after, betrayer) + " is out of it, the rest of you keep it, and nobody holds it against you."));
            Assert.That(line.audienceIds, Is.EquivalentTo(new[] { after.playerId, betrayer, loyal }), "Everybody in the pact hears it.");
            var page = AllianceRead.Yours(after).Single(p => p.id == PactId);
            Assert.That(page.ended, Is.Null, "The alliances page shows it standing,");
            Assert.That(page.members.Select(m => m.id), Is.EqualTo(new[] { loyal }), "without them.");
            Assert.That(BetrayersReplies, Does.Contain(HouseDialogue.Response(after, betrayer, EpisodeCommandKind.LeaveAlliance)), "and the betrayer knows why.");
            Assert.That(Allegiance.FreeExit(after, betrayer), Is.False, "Cut out, there is nothing left to leave.");
            Valid(after);
        }

        [Test]
        public void C2_CuttingTiesInAPactOfTwoEndsIt()
        {
            var s = PactOfThreeWithABetrayal(rules: true, out string betrayer, out _, three: false);
            uint stream = s.randomState;
            Assert.That(Allegiance.FreeExitKeepsAPact(s, betrayer), Is.False);
            var after = Leave(s, betrayer);
            Assert.That(after.alliances.Single(a => a.id == PactId).active, Is.False, "Ties are cut, and a pact of two is over.");
            Assert.That(Grudges.Severity(after, betrayer, after.playerId), Is.Zero, "No grudge,");
            Assert.That(after.randomState, Is.EqualTo(stream), "and no roll.");
            Assert.That(after.ledger.alliances.Single(r => r.id == PactId).why, Does.EndWith("/betrayed"), "The ledger row's end.");
            var line = after.events.Last();
            Assert.That(line.text, Is.EqualTo("You cut ties with " + after.Find(betrayer).name + " after they turned on " + PactName + ". It is over, and nobody holds it against you."));
            Assert.That(line.audienceIds, Is.EquivalentTo(new[] { after.playerId, betrayer }));
            Assert.That(AllianceRead.Yours(after).Single(p => p.id == PactId).ended, Is.EqualTo(AllianceRead.CutTies), "The alliances page says how it ended,");
            var sense = GameSense.Evaluate(after).notes.Single(n => n.rowKind == "alliance" && n.rowId == PactId);
            Assert.That(sense.text, Is.EqualTo("Week " + after.week + ": your alliance with " + after.Find(betrayer).name + " ended in a betrayal."), "and so does Game Sense,");
            Assert.That(sense.points, Is.EqualTo(-6));
            Assert.That(sense.known, Is.True, "from a betrayal the player can know.");
            Assert.That(BetrayersReplies, Does.Contain(HouseDialogue.Response(after, betrayer, EpisodeCommandKind.LeaveAlliance)), "The betrayer knows why.");
            Valid(after);
        }

        /// <summary>What a betrayer says when the player cuts ties with them (HouseDialogue.Response).</summary>
        private static readonly object[] BetrayersReplies =
        {
            "That's fair. I made my choice first; you're only answering it.",
            "Fine. I'd have done the same in your shoes.",
            "I know. I'd cut me off too. I'm sorry it came to this.",
            "Yeah. I figured that was coming. No hard feelings, honestly.",
            "Understood. My move cost me your trust. That was the price.",
            "Fair enough. I gave you every reason to.",
        };

        [Test]
        public void C2_TheFreeExitIsGoneTheNextWeekAndLeavingCostsWhatItAlwaysDid()
        {
            var s = PactOfThreeWithABetrayal(rules: true, out string betrayer, out string loyal);
            s.week = 2;
            Assert.That(Allegiance.FreeExit(s, betrayer), Is.False, "The window was the week of the betrayal.");
            var paid = Leave(s, betrayer);
            var plain = Leave(PactOfThreeWithABetrayal(rules: true, out _, out _, betray: false, week: 2), betrayer);
            Assert.That(Grudges.Severity(paid, loyal, paid.playerId), Is.EqualTo(80), "The others hold the web's eighty against the player, as always.");
            Assert.That(Grudges.Severity(paid, betrayer, paid.playerId), Is.EqualTo(80));
            Assert.That(EpisodeEngine.SocialActionsSpent(paid), Is.EqualTo(1), "It is a social action again.");
            Assert.That(paid.Score(betrayer, paid.playerId), Is.EqualTo(plain.Score(betrayer, plain.playerId)), "The same -15 with them, through the same roll.");
            Assert.That(paid.randomState, Is.EqualTo(plain.randomState));
            // A pact of three goes on without the player (C5, the story's defector rule): the line says so.
            Assert.That(paid.events.Last().text, Is.EqualTo("You left the alliance with " + paid.Find(betrayer).name + " and "
                + paid.Find(loyal).name + ": " + PactName + " goes on without you."));
            Assert.That(paid.alliances.Single(a => a.id == PactId).members, Is.EqualTo(new[] { betrayer, loyal }), "The other two keep it.");
            Assert.That(BetrayersReplies, Does.Not.Contain(HouseDialogue.Response(paid, betrayer, EpisodeCommandKind.LeaveAlliance)));
        }

        /// <summary>
        /// Leaving outside a betrayal costs what it always cost: the same roll for the same −15 with the one
        /// told, the eighty from every member, an action. A pact of two ends as it always did, line for line;
        /// a pact of three or more goes on without the player under the rules (C5), where without them it
        /// ended for everybody.
        /// </summary>
        [Test]
        public void C2_LeavingOutsideABetrayalCostsWhatItAlwaysDid()
        {
            var offTwo = Leave(PactOfThreeWithABetrayal(rules: false, out string partner, out _, betray: false, three: false), partner);
            var onTwo = Leave(PactOfThreeWithABetrayal(rules: true, out _, out _, betray: false, three: false), partner);
            Assert.That(onTwo.randomState, Is.EqualTo(offTwo.randomState), "A pact of two: the same roll,");
            Assert.That(Json(onTwo.relationships), Is.EqualTo(Json(offTwo.relationships)));
            Assert.That(Json(onTwo.story.grudges), Is.EqualTo(Json(offTwo.story.grudges)));
            Assert.That(Json(onTwo.events), Is.EqualTo(Json(offTwo.events)), "the same line,");
            Assert.That(Json(onTwo.alliances), Is.EqualTo(Json(offTwo.alliances)), "and it ends, as it always did.");
            Assert.That(onTwo.alliances.Single(a => a.id == PactId).active, Is.False);

            var off = Leave(PactOfThreeWithABetrayal(rules: false, out string ally, out string loyal, betray: false), ally);
            var on = Leave(PactOfThreeWithABetrayal(rules: true, out _, out _, betray: false), ally);
            Assert.That(on.randomState, Is.EqualTo(off.randomState), "The same roll for the same -15.");
            Assert.That(Json(on.relationships), Is.EqualTo(Json(off.relationships)));
            Assert.That(Json(on.story.grudges), Is.EqualTo(Json(off.story.grudges)), "The same eighty from every member.");
            Assert.That(Grudges.Severity(on, loyal, on.playerId), Is.EqualTo(80));
            Assert.That(EpisodeEngine.SocialActionsSpent(on), Is.EqualTo(EpisodeEngine.SocialActionsSpent(off)));
            Assert.That(Allegiance.FreeExit(on, ally), Is.False);
            Assert.That(off.alliances.Single(a => a.id == PactId).active, Is.False, "Without the rules a pact of three ended for everybody;");
            var pact = on.alliances.Single(a => a.id == PactId);
            Assert.That(pact.active, Is.True, "under them it goes on");
            Assert.That(pact.members, Is.EqualTo(new[] { ally, loyal }), "without the player.");
        }

        // ------------------------------------------------------------ C2: what the player can know

        /// <summary>
        /// The vote read is the player's: two allies the player read this week, one of whom turned on them
        /// by a ballot the player cannot place, read alike - though the engine holds the betrayal, and the
        /// betrayer's ballot no longer weighs the pact. Once the player can place it, the read can too.
        /// </summary>
        [Test]
        public void C2_TheVoteReadDoesNotTellABallotBetrayalThePlayerCannotPlace()
        {
            var honest = ReadInWeekTwo(true, out string ally, out _);
            var betrayed = honest.Clone();
            BetrayIn(betrayed, ally, Allegiance.VotedAgainst, week: 1);
            var entry = Entries(betrayed, betrayed.playerId, ally, Allegiance.BetrayedType).Single();
            Assert.That(KnownBallots.TellsAnUnknownBallot(betrayed, ally, entry.description, entry.week), Is.True, "Precondition: a ballot the player cannot place.");
            Assert.That(AllianceFactor(honest, ally, honest.playerId), Is.GreaterThan(0), "The honest ally's ballot weighs the pact;");
            Assert.That(AllianceFactor(betrayed, ally, betrayed.playerId), Is.Zero, "the betrayer's does not: the engine holds what it knows.");
            Assert.That(VoteRead.CommitmentHidden(honest, ally) || VoteRead.CommitmentHidden(betrayed, ally), Is.False,
                "Precondition: the player read both this week, so the read counts their pact terms.");
            Assert.That(VoteRead.Read(honest).voters.Single(v => v.voterId == ally).knownTerms, Does.Contain("alliance"));
            Assert.That(Json(VoteRead.Read(betrayed)), Is.EqualTo(Json(VoteRead.Read(honest))), "The read cannot tell them apart.");

            // They told the player they would vote them out, and did: now the player can place it.
            betrayed.ledger.claims.Add(new ClaimRow { week = 1, voterId = ally, targetId = betrayed.playerId, source = ClaimSource.Told, status = ClaimStatus.Kept });
            Assert.That(KnownBallots.TellsAnUnknownBallot(betrayed, ally, entry.description, entry.week), Is.False);
            Assert.That(Json(VoteRead.Read(betrayed)), Is.Not.EqualTo(Json(VoteRead.Read(honest))), "Known, the betrayal is the read's.");
        }

        /// <summary>
        /// The deal table's alliance bonus and a plea's "you're allied" ask whether the ally's word holds;
        /// the odds the player is shown ask it as the player knows it. A betrayal they can see costs the
        /// bonus there too; one told only by a ballot they cannot place never moves what they are shown.
        /// </summary>
        [Test]
        public void C2_TheDealTableAndThePleaAskWhetherTheAllysWordHoldsAndTheShownOddsAskItAsThePlayerKnowsIt()
        {
            foreach (bool rules in new[] { false, true })
            {
                var clean = ReadInWeekTwo(rules, out string ally, out _);
                var hidden = clean.Clone();
                BetrayIn(hidden, ally, Allegiance.VotedAgainst, week: 1);
                var known = clean.Clone();
                BetrayIn(known, ally, Allegiance.Nominated, week: 1);
                double Roll(EpisodeState s) => PlayerDeals.AcceptanceChance(s, ally, DealKind.SafetyAgreement, null);
                double Plea(EpisodeState s) => StrategyRules.Chance(s, ally, LobbyAsk.Vote, s.playerId, LobbyApproach.Pressure);
                double ShownDeal(EpisodeState s) => KnownOdds.Deal(s, ally, DealKind.SafetyAgreement, null).chance;
                double ShownPlea(EpisodeState s) => KnownOdds.Plea(s, ally, LobbyAsk.Vote, s.playerId, LobbyApproach.Pressure).chance;
                if (!rules)
                {
                    foreach (var s in new[] { hidden, known })
                    {
                        Assert.That(Roll(s), Is.EqualTo(Roll(clean)), "Without the rules the record moves nothing.");
                        Assert.That(Plea(s), Is.EqualTo(Plea(clean)));
                        Assert.That(ShownDeal(s), Is.EqualTo(ShownDeal(clean)));
                        Assert.That(ShownPlea(s), Is.EqualTo(ShownPlea(clean)));
                    }
                    continue;
                }
                foreach (var s in new[] { hidden, known })
                {
                    Assert.That(Roll(s), Is.EqualTo(Roll(clean) - 30).Within(1e-9), "The deal table's +20, and +10 for a safety pact, are the ally's word's.");
                    Assert.That(Plea(s), Is.EqualTo(Plea(clean) - 15).Within(1e-9), "So is a plea's +15.");
                }
                Assert.That(ShownDeal(known), Is.EqualTo(ShownDeal(clean) - 30).Within(1e-9), "The odds shown know what the player knows,");
                Assert.That(ShownPlea(known), Is.EqualTo(ShownPlea(clean) - 15).Within(1e-9));
                Assert.That(ShownDeal(hidden), Is.EqualTo(ShownDeal(clean)), "and never what a ballot they cannot place would tell.");
                Assert.That(ShownPlea(hidden), Is.EqualTo(ShownPlea(clean)));
            }
        }

        // ------------------------------------------------------------ C3: the NPC's own commitment

        /// <summary>
        /// A partner who has gone cold on the player keeps none of their terms, and one who has turned
        /// ends the pact from their side as the week opens - where before the rules it stood on the
        /// player's own view alone, and the turned partner's +30 shield stood with it.
        /// </summary>
        [Test]
        public void C3_ATurnedPartnerEndsThePactAndTheirShieldEnds()
        {
            foreach (bool rules in new[] { false, true })
            {
                var before = EvictionResolvedWithAPartner(rules, out string partner, partnerView: NpcAlliances.SourLine - 5);
                before.strategyRulesStartWeek = 1;
                var unallied = before.Clone();
                unallied.alliances.RemoveAll(a => a.id == PactId);
                double reluctance = StrategyRules.NominationReluctance(before, partner, before.playerId);
                Assert.That(reluctance, Is.EqualTo(StrategyRules.NominationReluctance(unallied, partner, before.playerId) + (rules ? 0 : StrategyRules.AllyShield)),
                    rules ? "Under the rules the shield is theirs to give, and they have turned." : "Without them the turned partner's shield stood.");
                var added = OpenTheSocialWeek(before, out var after);
                var pact = after.alliances.Single(a => a.id == PactId);
                if (!rules)
                {
                    Assert.That(pact.active, Is.True, "Without the rules a partner's feeling never ended the player's pact.");
                    continue;
                }
                Assert.That(pact.active, Is.False, "They ended it from their side.");
                var told = added.Where(e => e.kind == "alliance").ToList();
                Assert.That(told.Select(e => e.text), Is.EqualTo(new[] { "Your alliance with " + after.Find(partner).name + " has fallen apart." }),
                    "In words, the same however it soured.");
                Assert.That(told[0].text.Any(char.IsDigit), Is.False, "and with no number.");
                Assert.That(after.ledger.alliances.Single(r => r.id == PactId).why, Does.EndWith("/turned"), "The ledger says the partner turned.");
            }
        }

        /// <summary>
        /// An ally gone cold keeps none of their terms. The player can tell only through something this
        /// week - contact, a read of them, a call they refused - and only then do the notes say "gone
        /// quiet", in words, and the vote read count their pact terms: one rule, so the two agree.
        /// </summary>
        [Test]
        public void C3_AnAllyGoneQuietKeepsNoTermsAndTheNotesSayItOnceThePlayerCanTell()
        {
            foreach (bool rules in new[] { false, true })
            {
                var s = Protected(rules, out string ally, out string other);
                SetScore(s, ally, s.playerId, Allegiance.QuietLine - 5);
                if (!rules)
                {
                    Touch(s, ally);
                    Assert.That(Quiet(s, ally), Is.False);
                    Assert.That(Allegiance.Holds(s, ally, s.playerId), Is.True, "Without the rules the pact holds whatever they think.");
                    continue;
                }
                Assert.That(Allegiance.Holds(s, ally, s.playerId), Is.False);
                Assert.That(StrategyRules.NominationReluctance(s, ally, s.playerId), Is.EqualTo(s.Score(ally, s.playerId)), "No shield,");
                Assert.That(AllianceFactor(s, ally, s.playerId), Is.Zero, "no loyalty in their ballot,");
                Assert.That(NpcAlliances.ActiveAlliancesFor(s, ally).Single().active, Is.True, "while the pact itself still stands above the line it sours at.");
                Assert.That(Quiet(s, ally), Is.False, "Nothing this week has shown the player where they stand, so nothing says it,");
                Assert.That(VoteRead.CommitmentHidden(s, ally), Is.True, "and the read keeps their pact terms theirs.");

                var signals = new (string how, Action<EpisodeState> show)[]
                {
                    ("contact this week", t => Touch(t, ally)),
                    ("a read this week", t => t.ledger.standings.Add(new StandingRow { week = t.week, fromId = ally, toId = t.playerId, source = ClaimSource.Read, score = t.Score(ally, t.playerId) })),
                    ("a refused call this week", t => t.ledger.calls.Add(new BlocCallRow { week = t.week, allianceId = PactId, callerId = t.playerId, targetId = other, defected = new List<string> { ally } })),
                };
                foreach (var signal in signals)
                {
                    var told = s.Clone();
                    signal.show(told);
                    var note = HouseguestNotes.For(told, ally).Single(n => n.text.Contains("gone quiet"));
                    Assert.That(note.text, Is.EqualTo(First(told, ally) + " has gone quiet on " + PactName), signal.how);
                    Assert.That(note.text.Any(char.IsDigit), Is.False, "Never a number.");
                    Assert.That(note.kind, Is.EqualTo(HouseguestNotes.Kinds.Pact));
                    Assert.That(VoteRead.CommitmentHidden(told, ally), Is.False, "The read counts their terms when the notes speak: " + signal.how);
                    SetScore(told, ally, told.playerId, Allegiance.QuietLine + 5);
                    Assert.That(Quiet(told, ally), Is.False, "Above the line, nothing to say.");
                    Assert.That(Allegiance.Holds(told, ally, told.playerId), Is.True);
                }
            }
        }

        [Test]
        public void C3_AMemberWhoseCommitmentHasLapsedIgnoresTheCallWithoutARoll()
        {
            foreach (bool rules in new[] { false, true })
            {
                // Loyalty in the band a roll decides (30 to 50): a warm pact, and a member cooling on the player.
                var before = CallInAPactOfThree(rules, looseView: Allegiance.QuietLine - 5, out string loyal, out string loose, out string target, warmPact: true);
                var engine = new EpisodeEngine(before);
                Assert.That(Apply(engine, EpisodeCommandKind.CallTheVote, loyal, target, PactId).accepted, Is.True);
                var after = engine.Snapshot;
                var call = after.ledger.calls.Single();
                Assert.That(call.followed, Does.Contain(loyal), "Precondition: the committed member follows, on loyalty alone.");
                if (rules)
                {
                    Assert.That(call.defected, Is.EqualTo(new[] { loose }), "A lapsed member does not follow:");
                    Assert.That(after.randomState, Is.EqualTo(before.randomState), "nothing was left to chance,");
                    Assert.That(Entries(after, after.playerId, loose, Allegiance.BetrayedType), Is.Empty,
                        "and the refusal alone turns on nothing: the ballot keeps or breaks it (C2).");
                }
                else Assert.That(after.randomState, Is.Not.EqualTo(before.randomState), "Without the rules their loyalty was a roll.");
            }
        }

        /// <summary>
        /// An ally's commitment is theirs: knowing the pact does not tell the vote read whether they are
        /// still with you, so two allies the player has not read - one committed, one gone cold - read
        /// alike. Reading them is how the read learns it.
        /// </summary>
        [Test]
        public void C3_TheVoteReadDoesNotTellAnAllysCommitmentUntilThePlayerReadsThem()
        {
            var committed = Protected(true, out string ally, out string other);
            // The player heard how the ally sees the other nominee, and nothing of how they see the player.
            committed.ledger.standings.Add(new StandingRow { week = committed.week, fromId = ally, toId = other, source = ClaimSource.Told, score = 0 });
            var lapsed = committed.Clone();
            SetScore(lapsed, ally, lapsed.playerId, Allegiance.QuietLine - 5);
            Assert.That(AllianceFactor(committed, ally, committed.playerId), Is.GreaterThan(0), "Precondition: the committed ally's ballot weighs the pact,");
            Assert.That(AllianceFactor(lapsed, ally, lapsed.playerId), Is.Zero, "and the cold one's does not.");
            Assert.That(VoteRead.CommitmentHidden(committed, ally) && VoteRead.CommitmentHidden(lapsed, ally), Is.True);
            var a = Read(committed, ally);
            var b = Read(lapsed, ally);
            Assert.That(b.leaningId, Is.EqualTo(a.leaningId), "The read cannot tell them apart:");
            Assert.That(b.confidence, Is.EqualTo(a.confidence));
            Assert.That(b.knownMargin, Is.EqualTo(a.knownMargin));
            Assert.That(b.knownTerms, Is.EqualTo(a.knownTerms));
            Assert.That(b.unknownTerms, Is.EqualTo(a.unknownTerms), "not even by how much it cannot see.");
            Assert.That(a.exact, Is.False, "and it shows no number while the ally's commitment is theirs.");

            // Read them, and the read is the player's: now it knows where each stands.
            foreach (var state in new[] { committed, lapsed })
                state.ledger.standings.Add(new StandingRow { week = state.week, fromId = ally, toId = state.playerId, source = ClaimSource.Read, score = state.Score(ally, state.playerId) });
            Assert.That(VoteRead.CommitmentHidden(committed, ally), Is.False);
            var c = Read(committed, ally);
            var d = Read(lapsed, ally);
            Assert.That(c.unknownTerms, Is.LessThan(a.unknownTerms), "The pact's term is the read's once the player knows where they stand.");
            Assert.That(c.knownMargin != d.knownMargin || c.leaningId != d.leaningId, Is.True, "A read is how the player learns an ally has gone cold.");
        }

        // ------------------------------------------------------------ X5: the evicted leave every pact

        [Test]
        public void X5_EvicteesLeavePactsOfThreeOrMoreAndTheJuryReads25()
        {
            foreach (bool rules in new[] { false, true })
            {
                var s = PactOfThreeInFreeTime(rules, out string ally, out string third);
                var evaluator = Npcs(s).First(n => n.id != ally && n.id != third).id;
                s.Find(ally).status = ContestantStatus.Jury;
                Valid(s);
                var pact = s.alliances.Single(a => a.id == PactId);
                Assert.That(pact.active, Is.True, "A pact of three goes on with two left in the house.");
                Assert.That(pact.members, Does.Contain(ally), "and its record still names who was in it.");
                Assert.That(s.Allied(s.playerId, third), Is.True);
                double jury = WebJuryVoting.AllianceLoyalty(s, ally, s.playerId);
                double threat = ThreatAssessment.Assess(s, evaluator, s.playerId).Alliance;
                if (rules)
                {
                    Assert.That(s.Allied(ally, s.playerId) || s.Allied(ally, third), Is.False, "The evictee has left it.");
                    Assert.That(jury, Is.EqualTo(25), "The jury reads 25 for a juror out of a pact.");
                    Assert.That(threat, Is.EqualTo(8), "The bloc is the two still in it.");
                    Assert.That(Allegiance.Holds(s, ally, s.playerId), Is.False);
                }
                else
                {
                    Assert.That(s.Allied(ally, s.playerId), Is.True, "Without the rules the evictee stayed in it (X5),");
                    Assert.That(jury, Is.EqualTo(100), "and the jury read 100.");
                    Assert.That(threat, Is.EqualTo(12));
                }
                var note = HouseguestNotes.For(s, ally).Single(n => n.kind == HouseguestNotes.Kinds.Pact);
                Assert.That(note.text, Is.EqualTo(rules ? "You were both in " + PactName + " · " + First(s, ally) + " left the house" : "You are both in " + PactName));
            }

            // The player leaves every pact with the house too, and the notes say who left.
            var mine = PactOfThreeInFreeTime(true, out string friend, out _);
            mine.Find(mine.playerId).status = ContestantStatus.Jury;
            Assert.That(PactNote(mine, friend), Is.EqualTo("You were both in " + PactName + " · you left the house"));
            mine.Find(friend).status = ContestantStatus.Jury;
            Assert.That(PactNote(mine, friend), Is.EqualTo("You were both in " + PactName + " · you both left the house"));
        }

        [Test]
        public void X5_TheEvictionVoteCountsOnlyTheMembersStillInTheHouse()
        {
            foreach (bool rules in new[] { false, true })
            {
                var s = Protected(rules, out string ally, out string third, pactOfThree: true);
                s.Find(third).status = ContestantStatus.Jury;
                var dto = WebEvictionVoting.FromNative(s, ally).state.alliances.Single(a => a.id == PactId);
                Assert.That(dto.members, Is.EquivalentTo(rules ? new[] { s.playerId, ally } : new[] { s.playerId, ally, third }));
            }

            // A pact the player has left the house for is one among houseguests: nobody's commitment
            // to the player decides who answers to its bloc any more.
            var gone = PactOfThreeInFreeTime(true, out string cold, out _);
            var pact = gone.alliances.Single(a => a.id == PactId);
            SetScore(gone, cold, gone.playerId, Allegiance.QuietLine - 30);
            Assert.That(Allegiance.LapsedMembers(gone, pact), Is.EqualTo(new[] { cold }));
            gone.Find(gone.playerId).status = ContestantStatus.Jury;
            Assert.That(Allegiance.LapsedMembers(gone, pact), Is.Empty);
            Assert.That(Allegiance.Following(gone, pact), Is.EqualTo(pact.members));
        }

        // ------------------------------------------------------------ the two terms C0 left

        [Test]
        public void ABrokenDealPushesANominationOrABallotOnlyAgainstWhoeverBrokeIt()
        {
            foreach (bool rules in new[] { false, true })
            {
                var s = Protected(rules, out string ally, out _);
                // A safety pact between the ally and the player that the ally broke.
                s.deals.Add(BrokenDeal(s, "deal-theirs", s.playerId, ally, ally, rules));
                double clean = StrategyRules.NominationReluctance(Without(s, "deal-theirs"), ally, s.playerId);
                double held = StrategyRules.NominationReluctance(s, ally, s.playerId);
                double cleanVote = DealFactor(Without(s, "deal-theirs"), ally, s.playerId);
                double vote = DealFactor(s, ally, s.playerId);
                if (rules)
                {
                    Assert.That(held, Is.EqualTo(clean), "Their own breach is no grievance of theirs against the one they wronged.");
                    Assert.That(vote, Is.EqualTo(cleanVote), "nor in their ballot.");
                }
                else
                {
                    Assert.That(held, Is.EqualTo(clean + StrategyRules.BrokenDealWeight), "Without the rules either side's breach pushed them on.");
                    Assert.That(vote, Is.LessThan(cleanVote));
                }
                // The player's breach counts either way.
                var mine = Protected(rules, out ally, out _);
                mine.deals.Add(BrokenDeal(mine, "deal-mine", mine.playerId, ally, mine.playerId, rules));
                Assert.That(StrategyRules.NominationReluctance(mine, ally, mine.playerId),
                    Is.EqualTo(StrategyRules.NominationReluctance(Without(mine, "deal-mine"), ally, mine.playerId) + StrategyRules.BrokenDealWeight));
                Assert.That(DealFactor(mine, ally, mine.playerId), Is.LessThan(DealFactor(Without(mine, "deal-mine"), ally, mine.playerId)));
            }
        }

        // ------------------------------------------------------------ fixtures

        private static EpisodeState Season(uint seed, int size = 8) =>
            SeasonBuilder.Create(new SeasonBuilder.Choice { HouseSize = size }, seed);

        private static List<ContestantState> Npcs(EpisodeState s) => s.Active.Where(c => !c.isPlayer).ToList();

        private static string First(EpisodeState s, string id) => s.Find(id).name.Split(' ')[0];

        private static string Json(object value) => JsonConvert.SerializeObject(value);

        private static void Valid(EpisodeState s) => Assert.That(EpisodeValidation.TryValidate(s, out var error), Is.True, error);

        private static void SetScore(EpisodeState s, string from, string to, double score)
        {
            var edge = s.relationships.FirstOrDefault(r => r.fromId == from && r.toId == to);
            if (edge == null) s.relationships.Add(edge = new RelationshipState { fromId = from, toId = to });
            edge.score = score;
        }

        /// <summary>The player had contact with them this week: their own record of them was touched.</summary>
        private static void Touch(EpisodeState s, string id) =>
            s.relationships.Single(r => r.fromId == s.playerId && r.toId == id).lastInteractionWeek = s.week;

        /// <summary>Whether the notes say this ally has gone quiet.</summary>
        private static bool Quiet(EpisodeState s, string id) =>
            HouseguestNotes.For(s, id).Any(n => n.kind == HouseguestNotes.Kinds.Pact && n.text.Contains("gone quiet"));

        /// <summary>The notes' line on the pact the player shares with them.</summary>
        private static string PactNote(EpisodeState s, string id) =>
            HouseguestNotes.For(s, id).Single(n => n.kind == HouseguestNotes.Kinds.Pact).text;

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

        /// <summary>The same, written in an earlier <paramref name="week"/>.</summary>
        private static void BetrayIn(EpisodeState s, string npcId, string act, int week)
        {
            int now = s.week;
            s.week = week;
            Betray(s, npcId, act);
            s.week = now;
        }

        private static EpisodeCommand Command(EpisodeState s, EpisodeCommandKind kind, string target = null, string second = null, string text = null) =>
            new EpisodeCommand
            {
                id = "betrayal-" + kind + "-" + s.revision, actorId = s.playerId, kind = kind,
                targetId = target, secondTargetId = second, text = text, expectedRevision = s.revision, expectedPhase = s.phase,
            };

        private static CommandResult Apply(EpisodeEngine engine, EpisodeCommandKind kind, string target = null, string second = null, string text = null) =>
            engine.Apply(Command(engine.Snapshot, kind, target, second, text));

        private static EpisodeState Advance(EpisodeState before)
        {
            var engine = new EpisodeEngine(before);
            var result = engine.Apply(EpisodeEngineTests.Command(before, EpisodeCommandKind.Advance));
            Assert.That(result.accepted, Is.True, result.reason);
            return engine.Snapshot;
        }

        /// <summary>The house plays to the reveal.</summary>
        private static EpisodeState Reveal(EpisodeState before)
        {
            var engine = new EpisodeEngine(before);
            for (int i = 0; i < 20 && !engine.Snapshot.evictionResolved; i++)
            {
                var result = engine.Apply(EpisodeEngineTests.NextCommand(engine.Snapshot));
                Assert.That(result.accepted, Is.True, result.reason);
            }
            Assert.That(engine.Snapshot.evictionResolved, Is.True);
            return engine.Snapshot;
        }

        private static EpisodeState Leave(EpisodeState s, string allyId)
        {
            var engine = new EpisodeEngine(s);
            var left = Apply(engine, EpisodeCommandKind.LeaveAlliance, allyId);
            Assert.That(left.accepted, Is.True, left.reason);
            return engine.Snapshot;
        }

        private static List<EpisodeEvent> Added(EpisodeState before, EpisodeState after) =>
            after.events.Where(e => e.sequence >= before.nextSequence).ToList();

        private static List<RelationshipEventState> Entries(EpisodeState s, string from, string to, string type) =>
            s.relationships.Where(r => r.fromId == from && r.toId == to).SelectMany(r => r.events).Where(e => e.type == type).ToList();

        /// <summary>The same season without one deal.</summary>
        private static EpisodeState Without(EpisodeState s, string id)
        {
            var copy = s.Clone();
            copy.deals.RemoveAll(d => d.id == id);
            return copy;
        }

        private static DealState BrokenDeal(EpisodeState s, string id, string proposer, string recipient, string breaker, bool recorded) =>
            new DealState
            {
                id = id, type = DealKind.SafetyAgreement, proposerId = proposer, recipientId = recipient,
                status = DealStatus.Broken, week = 1, expiresWeek = 2, trustImpact = DealKind.DefaultTrust(DealKind.SafetyAgreement),
                brokenById = recorded ? breaker : null, settledWeek = recorded ? 1 : 0,
            };

        /// <summary>A voter's alliance term toward a nominee, as the eviction evaluator weighs it.</summary>
        private static double AllianceFactor(EpisodeState s, string voterId, string nomineeId) => Factor(s, voterId, nomineeId, "alliance");

        private static double DealFactor(EpisodeState s, string voterId, string nomineeId) => Factor(s, voterId, nomineeId, "deal");

        private static VoteRead.VoterRead Read(EpisodeState s, string voterId) => VoteRead.ReadVoter(s, voterId, WebEvictionVoting.EvaluateNative(s, voterId));

        private static double Factor(EpisodeState s, string voterId, string nomineeId, string code) =>
            WebEvictionVoting.EvaluateNative(s, voterId).nomineeEvaluations.Single(n => n.nomineeId == nomineeId).factors.Single(f => f.code == code).value;

        /// <summary>
        /// The nominations, with a Head of Household who likes everybody else and not the player - in a
        /// pact with them unless <paramref name="pact"/> is false: they put the player up.
        /// </summary>
        private static EpisodeState NominationsByAnAlly(bool rules, out string hoh, bool pact = true)
        {
            var s = Season(29);
            var npcs = Npcs(s);
            hoh = npcs[0].id;
            s.phase = EpisodePhase.Nomination;
            s.hohId = hoh;
            foreach (var other in s.contestants.Where(c => c.id != npcs[0].id)) SetScore(s, npcs[0].id, other.id, other.isPlayer ? -90 : 60);
            if (pact) Pact(s, PactId, PactName, s.playerId, hoh);
            if (rules) EpisodeEngine.EnableCommitments(s);
            Valid(s);
            return s;
        }

        /// <summary>The veto meeting: a nominee holds the veto and saves themselves, and the Head of Household - the player's ally, cold on them - names the replacement.</summary>
        private static EpisodeState VetoMeetingWithTheHeadAsAnAlly(bool rules, out string hoh)
        {
            var s = Season(31);
            var npcs = Npcs(s);
            hoh = npcs[0].id;
            s.phase = EpisodePhase.VetoMeeting;
            s.hohId = hoh;
            s.nominees = new List<string> { npcs[1].id, npcs[2].id };
            s.vetoHolderId = npcs[1].id;
            s.vetoPlayers = new List<string> { hoh, npcs[1].id, npcs[2].id, npcs[3].id, npcs[4].id, npcs[5].id };
            foreach (var other in s.contestants.Where(c => c.id != npcs[0].id)) SetScore(s, npcs[0].id, other.id, other.isPlayer ? -90 : 60);
            Pact(s, PactId, PactName, s.playerId, hoh);
            if (rules) EpisodeEngine.EnableCommitments(s);
            Valid(s);
            return s;
        }

        /// <summary>The veto meeting with the player on the block and an ally holding the veto, who owes them its use and is cold on everybody.</summary>
        private static EpisodeState VetoMeetingWithAnAllyHolding(bool rules, out string holder)
        {
            var s = Season(37);
            var npcs = Npcs(s);
            holder = npcs[3].id;
            s.phase = EpisodePhase.VetoMeeting;
            s.hohId = npcs[0].id;
            s.nominees = new List<string> { s.playerId, npcs[1].id };
            s.vetoHolderId = holder;
            s.vetoPlayers = new List<string> { npcs[0].id, s.playerId, npcs[1].id, holder, npcs[4].id, npcs[5].id };
            SetScore(s, holder, s.playerId, -50);
            SetScore(s, holder, npcs[1].id, -50);
            s.deals.Add(new DealState
            {
                id = "deal-veto", type = DealKind.VetoUse, proposerId = s.playerId, recipientId = holder, status = DealStatus.Active,
                week = 1, expiresWeek = 1, trustImpact = DealKind.DefaultTrust(DealKind.VetoUse),
            });
            Pact(s, PactId, PactName, s.playerId, holder);
            if (rules) EpisodeEngine.EnableCommitments(s);
            Valid(s);
            return s;
        }

        /// <summary>
        /// The campaign with a pact of three voting: a member who trusts the player and one whose view of
        /// them is <paramref name="looseView"/>. With <paramref name="warmPact"/> the pact is warm enough
        /// that the cooler member's loyalty falls in the band a roll decides. With
        /// <paramref name="looseSparesTarget"/> the cooler member likes the target - too much to follow a
        /// call to evict them - and is cold on the other nominee, whom the rest of the house likes. With
        /// <paramref name="looseVotesTargetAnyway"/> they hold a grudge against the target that sinks their
        /// loyalty to the call, and want the target out themselves.
        /// </summary>
        private static EpisodeState CallInAPactOfThree(bool rules, double looseView, out string loyal, out string loose, out string target,
            bool warmPact = false, bool looseSparesTarget = false, bool looseVotesTargetAnyway = false)
        {
            var s = ContentCatalog.Create(7);
            s.strategyRulesStartWeek = 1;
            EpisodeEngine.EnableLevers(s);
            var npcs = Npcs(s).Select(c => c.id).ToList();
            s.phase = EpisodePhase.Campaign;
            s.hohId = npcs[0];
            s.nominees = new List<string> { npcs[1], npcs[2] };
            s.vetoHolderId = npcs[3];
            s.vetoPlayers = s.Active.Select(c => c.id).Take(EpisodeEngine.VetoPlayerCount(s.Active.Count())).ToList();
            if (!s.vetoPlayers.Contains(s.vetoHolderId)) s.vetoPlayers[s.vetoPlayers.Count - 1] = s.vetoHolderId;
            s.vetoResolved = true;
            loyal = npcs[3]; loose = npcs[4]; target = npcs[1];
            s.alliances.RemoveAll(a => a.members.Contains(npcs[3]) || a.members.Contains(npcs[4]));
            var pact = Pact(s, PactId, PactName, s.playerId, loyal, loose);
            foreach (var id in pact.members) foreach (var other in pact.members.Where(o => o != id)) SetScore(s, id, other, warmPact ? 100 : 20);
            SetScore(s, loyal, s.playerId, 80); SetScore(s, loyal, target, 0);
            SetScore(s, loose, s.playerId, looseView); SetScore(s, loose, target, 0);
            if (looseSparesTarget)
            {
                SetScore(s, loose, target, 100);
                SetScore(s, loose, npcs[2], -100);
                foreach (var voter in npcs.Skip(5))
                {
                    SetScore(s, voter, target, -100);
                    SetScore(s, voter, npcs[2], 100);
                }
            }
            if (looseVotesTargetAnyway)
            {
                SetScore(s, loose, target, -100);
                SetScore(s, loose, npcs[2], 100);
                if (s.story == null) s.story = new StoryWorldState();
                Assert.That(Grudges.Add(s, loose, target, 60, GrudgeCauses.Story), Is.Not.Null);
            }
            if (rules) EpisodeEngine.EnableCommitments(s);
            Valid(s);
            return s;
        }

        /// <summary>
        /// The campaign with the player on the block beside <paramref name="other"/>: the house would keep
        /// the player (unless <paramref name="houseKeeps"/> is false), but their ally, who struck a vote to
        /// save them, would see them go.
        /// </summary>
        private static EpisodeState CampaignWithThePlayerUp(bool rules, out string ally, out string other, bool houseKeeps = true)
        {
            var s = Season(61);
            EpisodeEngine.EnableLevers(s);
            var npcs = Npcs(s);
            s.phase = EpisodePhase.Campaign;
            s.hohId = npcs[0].id;
            s.nominees = new List<string> { s.playerId, npcs[1].id };
            s.vetoHolderId = npcs[0].id;
            s.vetoPlayers = new List<string> { npcs[0].id, s.playerId, npcs[1].id, npcs[2].id, npcs[3].id, npcs[4].id };
            s.vetoResolved = true;
            ally = npcs[2].id;
            other = npcs[1].id;
            foreach (var voter in npcs.Skip(2))
            {
                bool turning = voter.id == ally || !houseKeeps;
                SetScore(s, voter.id, s.playerId, turning ? -100 : 100);
                SetScore(s, voter.id, other, turning ? 100 : -100);
            }
            Pact(s, PactId, PactName, s.playerId, ally);
            s.deals.Add(new DealState
            {
                id = "deal-vote", type = DealKind.VoteSave, proposerId = s.playerId, recipientId = ally, targetId = s.playerId,
                status = DealStatus.Active, week = 1, expiresWeek = 1, trustImpact = DealKind.DefaultTrust(DealKind.VoteSave),
            });
            if (rules) EpisodeEngine.EnableCommitments(s);
            Valid(s);
            return s;
        }

        /// <summary>
        /// The campaign with two others on the block, <paramref name="target"/> and <paramref name="spared"/>,
        /// and a vote to evict <paramref name="target"/> that the player's ally proposed - though they like
        /// <paramref name="target"/>, dislike <paramref name="spared"/> and think nothing of the player's
        /// word. The rest of the house votes <paramref name="target"/> out.
        /// </summary>
        private static EpisodeState CampaignWithAVoteDeal(bool rules, out string ally, out string target, out string spared)
        {
            var s = Season(61);
            EpisodeEngine.EnableLevers(s);
            var npcs = Npcs(s);
            s.phase = EpisodePhase.Campaign;
            s.hohId = npcs[0].id;
            target = npcs[1].id;
            spared = npcs[2].id;
            s.nominees = new List<string> { target, spared };
            s.vetoHolderId = npcs[0].id;
            s.vetoPlayers = new List<string> { npcs[0].id, s.playerId, target, spared, npcs[3].id, npcs[4].id };
            s.vetoResolved = true;
            ally = npcs[3].id;
            foreach (var voter in npcs.Skip(3))
            {
                bool breaking = voter.id == ally;
                SetScore(s, voter.id, target, breaking ? 100 : -100);
                SetScore(s, voter.id, spared, breaking ? -100 : 100);
            }
            SetScore(s, ally, s.playerId, 0);
            Pact(s, PactId, PactName, s.playerId, ally);
            s.deals.Add(new DealState
            {
                id = "deal-evict", type = DealKind.VoteEvict, proposerId = ally, recipientId = s.playerId, targetId = target,
                status = DealStatus.Active, week = 1, expiresWeek = 1, trustImpact = DealKind.DefaultTrust(DealKind.VoteEvict),
            });
            if (rules) EpisodeEngine.EnableCommitments(s);
            Valid(s);
            return s;
        }

        /// <summary>
        /// The campaign, with a committed ally (warm on the player) and the player on the block beside
        /// <paramref name="other"/>, so the ally's ballot can be read. The ally is not Sneaky, whose
        /// ballot weighs deals at nothing; with <paramref name="pactOfThree"/> the pact's third member is
        /// <paramref name="other"/> instead.
        /// </summary>
        private static EpisodeState Protected(bool rules, out string ally, out string other, bool pactOfThree = false)
        {
            var s = Season(43);
            s.strategyRulesStartWeek = 1;
            var npcs = Npcs(s);
            ally = npcs.Skip(2).First(c => !c.traits.Contains("Sneaky")).id;
            other = npcs[1].id;
            s.phase = EpisodePhase.Campaign;
            s.hohId = npcs[0].id;
            s.nominees = new List<string> { s.playerId, other };
            s.vetoHolderId = npcs[0].id;
            s.vetoPlayers = new List<string> { npcs[0].id, s.playerId, other, npcs[2].id, npcs[3].id, npcs[4].id };
            s.vetoResolved = true;
            SetScore(s, ally, s.playerId, 40);
            SetScore(s, ally, other, 0);
            SetScore(s, s.playerId, ally, 40);
            if (pactOfThree)
            {
                string allyId = ally;
                other = npcs.Skip(2).First(c => c.id != allyId).id;
                Pact(s, PactId, PactName, s.playerId, ally, other);
                foreach (var id in new[] { s.playerId, ally, other })
                    foreach (var to in new[] { s.playerId, ally, other }.Where(t => t != id)) SetScore(s, id, to, 40);
            }
            else Pact(s, PactId, PactName, s.playerId, ally);
            if (rules) EpisodeEngine.EnableCommitments(s);
            Valid(s);
            return s;
        }

        /// <summary>The campaign of <see cref="Protected"/> in week 2, the pact made in week 1, with the ally read this week.</summary>
        private static EpisodeState ReadInWeekTwo(bool rules, out string ally, out string other)
        {
            var s = Protected(rules, out ally, out other);
            s.week = 2;
            s.ledger.standings.Add(new StandingRow { week = 2, fromId = ally, toId = s.playerId, source = ClaimSource.Read, score = s.Score(ally, s.playerId) });
            return s;
        }

        /// <summary>Free time with a warm pact of three: the player, <paramref name="ally"/> and <paramref name="third"/>.</summary>
        private static EpisodeState PactOfThreeInFreeTime(bool rules, out string ally, out string third)
        {
            var s = Season(43);
            var npcs = Npcs(s);
            ally = npcs[2].id;
            third = npcs[3].id;
            Pact(s, PactId, PactName, s.playerId, ally, third);
            foreach (var id in new[] { s.playerId, ally, third })
                foreach (var to in new[] { s.playerId, ally, third }.Where(t => t != id)) SetScore(s, id, to, 40);
            if (rules) EpisodeEngine.EnableCommitments(s);
            Valid(s);
            return s;
        }

        /// <summary>
        /// Free time, with the story's grudges on and a pact of three - of two, the player and the
        /// betrayer, without <paramref name="three"/>: one member turned on it this week
        /// (<paramref name="betray"/>), the other is loyal. The week is <paramref name="week"/>.
        /// </summary>
        private static EpisodeState PactOfThreeWithABetrayal(bool rules, out string betrayer, out string loyal, bool betray = true, int week = 1, bool three = true)
        {
            var s = Season(47);
            EpisodeEngine.EnableStory(s);
            var npcs = Npcs(s);
            betrayer = npcs[0].id;
            loyal = npcs[1].id;
            var members = three ? new[] { s.playerId, betrayer, loyal } : new[] { s.playerId, betrayer };
            Pact(s, PactId, PactName, members);
            foreach (var id in members)
                foreach (var to in members.Where(t => t != id)) SetScore(s, id, to, 30);
            if (rules) EpisodeEngine.EnableCommitments(s);
            if (betray) Betray(s, betrayer, Allegiance.Nominated);
            s.week = week;
            Valid(s);
            return s;
        }

        /// <summary>A real season walked to its first resolved eviction with the player still in the house, a pact with a partner whose view of them is <paramref name="partnerView"/>.</summary>
        private static EpisodeState EvictionResolvedWithAPartner(bool rules, out string partner, double partnerView)
        {
            var engine = new EpisodeEngine(ContentCatalog.Create(3));
            for (int guard = 0; guard < 260 && !(engine.Snapshot.phase == EpisodePhase.Eviction && engine.Snapshot.evictionResolved); guard++)
            {
                var result = engine.Apply(EpisodeEngineTests.NextCommand(engine.Snapshot));
                Assert.That(result.accepted, Is.True, result.reason);
            }
            var s = engine.Snapshot;
            Assert.That(s.phase == EpisodePhase.Eviction && s.evictionResolved && s.Find(s.playerId).status == ContestantStatus.Active, Is.True,
                "Precondition: a resolved eviction the player survived.");
            Assert.That(NpcSocialState.AutonomyHasBegun(s), Is.True, "Precondition: the settle runs.");
            partner = s.Active.First(c => !c.isPlayer).id;
            Pact(s, PactId, PactName, s.playerId, partner);
            SetScore(s, s.playerId, partner, 40);
            SetScore(s, partner, s.playerId, partnerView);
            if (rules) EpisodeEngine.EnableCommitments(s);
            Valid(s);
            return s;
        }

        private static List<EpisodeEvent> OpenTheSocialWeek(EpisodeState before, out EpisodeState after)
        {
            after = Advance(before);
            Assert.That(after.phase, Is.EqualTo(EpisodePhase.Social));
            return Added(before, after);
        }
    }
}
