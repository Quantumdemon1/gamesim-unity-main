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

        [Test]
        public void C2_AnAllyWhoIgnoresTheCallFlagsThePact()
        {
            foreach (bool rules in new[] { false, true })
            {
                var before = CallInAPactOfThree(rules, looseView: -60, out string loyal, out string loose, out string target);
                var engine = new EpisodeEngine(before);
                var called = Apply(engine, EpisodeCommandKind.CallTheVote, loyal, target, PactId);
                Assert.That(called.accepted, Is.True, called.reason);
                var after = engine.Snapshot;
                var call = after.ledger.calls.Single();
                Assert.That(call.defected, Is.EqualTo(new[] { loose }), "Precondition: one of them ignored the call.");
                var entries = Entries(after, after.playerId, loose, Allegiance.BetrayedType);
                if (!rules) { Assert.That(entries, Is.Empty); continue; }
                Assert.That(entries.Single().description,
                    Is.EqualTo(after.Find(loose).name + " ignored your call to evict " + after.Find(target).name + " despite " + PactName + "."));
                var line = Added(before, after).Single(e => e.kind == Allegiance.LogKind);
                Assert.That(line.audienceIds, Is.EquivalentTo(new[] { after.playerId, loose }), "The call was refused to the player's face.");
                Assert.That(Entries(after, after.playerId, loyal, Allegiance.BetrayedType), Is.Empty, "Whoever followed turned on nothing.");
                Assert.That(Allegiance.FreeExit(after, loose), Is.True);
                Assert.That(Allegiance.FreeExit(after, loyal), Is.False);
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

        // ------------------------------------------------------------ C2: the player's choice

        [Test]
        public void C2_CuttingTiesTheWeekOfTheBetrayalCostsNothingAndNobodyHoldsAGrudge()
        {
            var s = PactOfThreeWithABetrayal(rules: true, out string betrayer, out string loyal);
            int spent = EpisodeEngine.SocialActionsSpent(s);
            uint stream = s.randomState;
            double theirs = s.Score(betrayer, s.playerId);
            Assert.That(Allegiance.FreeExit(s, betrayer), Is.True);
            var engine = new EpisodeEngine(s);
            var left = Apply(engine, EpisodeCommandKind.LeaveAlliance, betrayer);
            Assert.That(left.accepted, Is.True, left.reason);
            var after = engine.Snapshot;
            Assert.That(after.alliances.Single(a => a.id == PactId).active, Is.False, "Ties are cut.");
            Assert.That(Grudges.Severity(after, betrayer, after.playerId), Is.Zero, "No grudge from the betrayer,");
            Assert.That(Grudges.Severity(after, loyal, after.playerId), Is.Zero, "and none from the others: no 80 grudge on a betrayal exit.");
            Assert.That(after.Score(betrayer, after.playerId), Is.EqualTo(theirs), "No warmth lost with them,");
            Assert.That(after.randomState, Is.EqualTo(stream), "so no roll drawn,");
            Assert.That(EpisodeEngine.SocialActionsSpent(after), Is.EqualTo(spent), "and no action spent.");
            Assert.That(after.ledger.alliances.Single(r => r.id == PactId).why, Does.EndWith("/betrayed"), "The ledger row's end.");
            var line = after.events.Last();
            Assert.That(line.text, Is.EqualTo("You cut ties with " + after.Find(betrayer).name + " after they turned on " + PactName + ". Nobody holds it against you."));
            Assert.That(line.audienceIds, Is.EquivalentTo(new[] { after.playerId, betrayer, loyal }), "Everybody in the pact hears it.");
            Assert.That(AllianceRead.Yours(after).Single(p => p.id == PactId).ended, Is.EqualTo(AllianceRead.CutTies), "The alliances page says how it ended.");
            Assert.That(HouseDialogue.Response(after, betrayer, EpisodeCommandKind.LeaveAlliance), Is.AnyOf(BetrayersReplies), "and the betrayer knows why.");
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
            Assert.That(paid.events.Last().text, Is.EqualTo("You left the alliance with " + paid.Find(betrayer).name + "."));
            Assert.That(HouseDialogue.Response(paid, betrayer, EpisodeCommandKind.LeaveAlliance), Is.Not.AnyOf(BetrayersReplies));
        }

        [Test]
        public void C2_LeavingOutsideABetrayalIsUnchangedByTheRules()
        {
            var off = Leave(PactOfThreeWithABetrayal(rules: false, out string ally, out string loyal, betray: false), ally);
            var on = Leave(PactOfThreeWithABetrayal(rules: true, out _, out _, betray: false), ally);
            Assert.That(on.randomState, Is.EqualTo(off.randomState), "The same roll for the same -15.");
            Assert.That(Json(on.relationships), Is.EqualTo(Json(off.relationships)));
            Assert.That(Json(on.story.grudges), Is.EqualTo(Json(off.story.grudges)), "The same eighty from every member.");
            Assert.That(Grudges.Severity(on, loyal, on.playerId), Is.EqualTo(80));
            Assert.That(Json(on.events), Is.EqualTo(Json(off.events)));
            Assert.That(Json(on.alliances), Is.EqualTo(Json(off.alliances)));
            Assert.That(EpisodeEngine.SocialActionsSpent(on), Is.EqualTo(EpisodeEngine.SocialActionsSpent(off)));
            Assert.That(Allegiance.FreeExit(on, ally), Is.False);
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
                Assert.That(after.ledger.alliances.Single(r => r.id == PactId).why, Does.Not.EndWith("/left-house"));
            }
        }

        [Test]
        public void C3_AnAllyGoneQuietKeepsNoTermsAndTheNotesSayItInWords()
        {
            foreach (bool rules in new[] { false, true })
            {
                var s = Protected(rules, out string ally, out _);
                SetScore(s, ally, s.playerId, Allegiance.QuietLine - 5);
                bool quiet = HouseguestNotes.For(s, ally).Any(n => n.kind == HouseguestNotes.Kinds.Pact && n.text.Contains("gone quiet"));
                if (!rules)
                {
                    Assert.That(quiet, Is.False);
                    Assert.That(Allegiance.Holds(s, ally, s.playerId), Is.True, "Without the rules the pact holds whatever they think.");
                    continue;
                }
                var note = HouseguestNotes.For(s, ally).Single(n => n.text.Contains("gone quiet"));
                Assert.That(note.text, Is.EqualTo(First(s, ally) + " has gone quiet on " + PactName));
                Assert.That(note.text.Any(char.IsDigit), Is.False, "Never a number.");
                Assert.That(note.kind, Is.EqualTo(HouseguestNotes.Kinds.Pact));
                Assert.That(Allegiance.Holds(s, ally, s.playerId), Is.False);
                Assert.That(StrategyRules.NominationReluctance(s, ally, s.playerId), Is.EqualTo(s.Score(ally, s.playerId)), "No shield,");
                Assert.That(AllianceFactor(s, ally, s.playerId), Is.Zero, "no loyalty in their ballot,");
                Assert.That(NpcAlliances.ActiveAlliancesFor(s, ally).Single().active, Is.True, "while the pact itself still stands above the line it sours at.");
                SetScore(s, ally, s.playerId, Allegiance.QuietLine + 5);
                Assert.That(HouseguestNotes.For(s, ally).Any(n => n.text.Contains("gone quiet")), Is.False, "Above the line, nothing to say.");
                Assert.That(Allegiance.Holds(s, ally, s.playerId), Is.True);
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
                    Assert.That(after.randomState, Is.EqualTo(before.randomState), "nothing was left to chance.");
                    Assert.That(Entries(after, after.playerId, loose, Allegiance.BetrayedType), Has.Count.EqualTo(1), "and the refused call turns on the pact (C2).");
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
        /// The nominations, with a Head of Household in a pact with the player who likes everybody else
        /// and not the player: the ally puts them up.
        /// </summary>
        private static EpisodeState NominationsByAnAlly(bool rules, out string hoh)
        {
            var s = Season(29);
            var npcs = Npcs(s);
            hoh = npcs[0].id;
            s.phase = EpisodePhase.Nomination;
            s.hohId = hoh;
            foreach (var other in s.contestants.Where(c => c.id != npcs[0].id)) SetScore(s, npcs[0].id, other.id, other.isPlayer ? -90 : 60);
            Pact(s, PactId, PactName, s.playerId, hoh);
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
        /// that the cooler member's loyalty falls in the band a roll decides.
        /// </summary>
        private static EpisodeState CallInAPactOfThree(bool rules, double looseView, out string loyal, out string loose, out string target, bool warmPact = false)
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
            if (rules) EpisodeEngine.EnableCommitments(s);
            Valid(s);
            return s;
        }

        /// <summary>
        /// The campaign with the player on the block beside <paramref name="other"/>: the house would keep
        /// the player, but their ally, who struck a vote to save them, would see them go.
        /// </summary>
        private static EpisodeState CampaignWithThePlayerUp(bool rules, out string ally, out string other)
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
                bool turning = voter.id == ally;
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
        /// Free time, with the story's grudges on and a pact of three: one member turned on it this week
        /// (<paramref name="betray"/>), the other is loyal. The week is <paramref name="week"/>.
        /// </summary>
        private static EpisodeState PactOfThreeWithABetrayal(bool rules, out string betrayer, out string loyal, bool betray = true, int week = 1)
        {
            var s = Season(47);
            EpisodeEngine.EnableStory(s);
            var npcs = Npcs(s);
            betrayer = npcs[0].id;
            loyal = npcs[1].id;
            Pact(s, PactId, PactName, s.playerId, betrayer, loyal);
            foreach (var id in new[] { s.playerId, betrayer, loyal })
                foreach (var to in new[] { s.playerId, betrayer, loyal }.Where(t => t != id)) SetScore(s, id, to, 30);
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
