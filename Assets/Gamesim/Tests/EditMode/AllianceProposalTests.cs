using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Gamesim.Simulation;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// One way to form an alliance (ACTIONS-DEALS-ALLIANCES-PLAN C4), under the commitment rules:
    /// 'Propose an alliance' rolls once on the alliance invitation's odds, which the player is shown
    /// as they read them (V6), the grudge their own walk-out left included; a grudge of forty or more
    /// refuses whatever the roll; the player holds at most three pacts, and nobody invites them to a
    /// fourth; a no spends the action, so it is no longer a free look at how the houseguest sees the
    /// player (X9), and neither is the houseguest's greeting; and a pact the player comes into is on
    /// the ledger as a pact between houseguests is. Each is shown under the rules and, where it
    /// differs, as a season without them still plays it. Unity-free, so the dotnet subset runs it
    /// (Tools/SimulationTests). The screen's half is EpisodePlayModeTests.AllianceProposal.
    /// </summary>
    public sealed class AllianceProposalTests
    {
        // ------------------------------------------------------------ the roll, and the odds shown

        /// <summary>
        /// Where the player knows every term the roll reads - how the houseguest sees them is the
        /// player's own reading of them, and nothing on the houseguest's private record is about the
        /// player - the chance on the row is the roll's, and the proposal is exactly one draw from the
        /// season's stream against it: a yes when the draw is under it, a no otherwise, for houseguests
        /// across the roll's tiers and two dozen draws each.
        /// </summary>
        [Test]
        public void UnderTheRulesAProposalIsOneRollOfTheSeasonsStreamAgainstTheChanceThePlayerIsShown()
        {
            int yes = 0, no = 0;
            foreach (int who in new[] { 0, 1, 2 })
            foreach (double view in new[] { -60.0, -20, 5, 30, 55, 85 })
            for (uint draw = 1; draw <= 24; draw++)
            {
                var s = KnownAlike(Rules(Season(61)), who, view, out var npc);
                s.randomState = draw * 2654435761u;
                string where = npc.name + " at " + view + ", draw " + draw;
                var shown = Shown(s, npc.id);
                Assert.That(shown.chance, Is.EqualTo(PlayerDeals.AcceptanceChance(s, npc.id, DealKind.AllianceInvite, null)).Within(1e-9),
                    where + ": the player knows every term, so the chance shown is the roll's.");

                var stream = new SeededRandom(s.randomState);
                bool predicted = stream.NextDouble() * 100 < shown.chance;
                var result = Apply(new EpisodeEngine(s), EpisodeCommandKind.FormAlliance, npc.id);
                Assert.That(result.accepted, Is.True, where + ": the proposal is put, whatever the answer: " + result.reason);
                var after = result.state;
                Assert.That(after.Allied(s.playerId, npc.id), Is.EqualTo(predicted), where + ": the answer is the draw against the chance shown.");
                // A no is the proposal's draw alone; a yes is that and the warmth's reciprocal draw.
                if (predicted) stream.NextDouble();
                Assert.That(after.randomState, Is.EqualTo(stream.State), where + ": from the season's own stream, and one draw for the proposal.");
                if (predicted) yes++; else no++;
            }
            Assert.That(yes, Is.GreaterThan(0), "Some said yes.");
            Assert.That(no, Is.GreaterThan(0), "Some said no.");
        }

        /// <summary>
        /// The row's chance is the player's read (V6): two houseguests who see the player very
        /// differently, but whom the player knows alike, show the same chance, though the roll reads
        /// how each really sees them. It is the deal table's chance for the invitation, term for term.
        /// </summary>
        [Test]
        public void TwoHouseguestsWhoSeeThePlayerDifferentlyButWhomThePlayerKnowsAlikeShowTheSameChance()
        {
            var s = Rules(Season(62));
            var warm = Plain(Npcs(s)[0]);
            var cold = Plain(Npcs(s)[1]);
            Set(s, s.playerId, warm.id, 30); Set(s, s.playerId, cold.id, 30);
            Set(s, warm.id, s.playerId, 90); Set(s, cold.id, s.playerId, -90);

            Assert.That(PlayerDeals.AcceptanceChance(s, warm.id, DealKind.AllianceInvite, null),
                Is.GreaterThan(PlayerDeals.AcceptanceChance(s, cold.id, DealKind.AllianceInvite, null)),
                "The roll reads how each of them really sees the player.");
            Assert.That(Shown(s, cold.id).chance, Is.EqualTo(Shown(s, warm.id).chance).Within(1e-9));
            Assert.That(Shown(s, cold.id).word, Is.EqualTo(Shown(s, warm.id).word), "The player knows them alike, so is shown the same.");
            Assert.That(Shown(s, warm.id).chance, Is.EqualTo(KnownOdds.Deal(s, warm.id, DealKind.AllianceInvite, null).chance).Within(1e-9),
                "The row's chance is the deal table's for the invitation.");
        }

        // ------------------------------------------------------------ a no spends the action

        /// <summary>
        /// X9's free probe. Without the rules a houseguest who sees the player at under eight refused
        /// for nothing - no action, no roll - so pressing told the player that hidden number. Under the
        /// rules the proposal is put to anybody, and a no is a committed proposal: the action spent, one
        /// draw, a line saying they turned it down, no pact and no warmth moved either way.
        /// </summary>
        [Test]
        public void UnderTheRulesANoSpendsTheActionWhereItUsedToBeAFreeLookAtTheirView()
        {
            var free = Season(63);
            var asked = Npcs(free)[0];
            Set(free, asked.id, free.playerId, 7); Set(free, free.playerId, asked.id, 7);
            var probe = Apply(new EpisodeEngine(free), EpisodeCommandKind.FormAlliance, asked.id);
            Assert.That(probe.accepted, Is.False, "Without the rules a view under eight refuses before anything is spent.");
            Assert.That(probe.reason, Is.EqualTo("Build some trust before proposing an alliance."));
            Assert.That((probe.state.revision, probe.state.randomState, EpisodeEngine.SocialActionsSpent(probe.state)),
                Is.EqualTo((free.revision, free.randomState, EpisodeEngine.SocialActionsSpent(free))), "and nothing is spent: the look is free.");

            var s = Rules(Season(63));
            Set(s, asked.id, s.playerId, 7); Set(s, s.playerId, asked.id, 7);
            double chance = PlayerDeals.AcceptanceChance(s, asked.id, DealKind.AllianceInvite, null);
            s.randomState = Draw(s, false, chance);
            string expected = PlayerDeals.Reasoning(s, asked.id, DealKind.AllianceInvite, false);
            int ledger = s.relationships.Sum(r => r.events.Count);
            var stream = new SeededRandom(s.randomState);
            stream.NextDouble();

            var result = Apply(new EpisodeEngine(s), EpisodeCommandKind.FormAlliance, asked.id);
            Assert.That(result.accepted, Is.True, "Under the rules the proposal is put: " + result.reason);
            var after = result.state;
            Assert.That(after.revision, Is.EqualTo(s.revision + 1));
            Assert.That(EpisodeEngine.SocialActionsSpent(after), Is.EqualTo(EpisodeEngine.SocialActionsSpent(s) + 1), "The no spends the action.");
            Assert.That(after.randomState, Is.EqualTo(stream.State), "One draw, the proposal's.");
            Assert.That(after.Allied(s.playerId, asked.id), Is.False);
            Assert.That(after.alliances.Count, Is.EqualTo(s.alliances.Count), "No pact.");
            Assert.That((after.Score(asked.id, s.playerId), after.Score(s.playerId, asked.id)), Is.EqualTo((7.0, 7.0)), "No warmth moves either way.");
            Assert.That(after.relationships.Sum(r => r.events.Count), Is.EqualTo(ledger), "Nothing on the record.");
            var line = after.events.Last();
            Assert.That(line.kind, Is.EqualTo(RefusedKind), "Its own kind: not an alliance's line, formed or ended.");
            Assert.That(line.text, Is.EqualTo(asked.name + " turned down your alliance. “" + expected + "”"));
            Assert.That(line.audienceIds, Is.EqualTo(new[] { s.playerId, asked.id }), "Between the two of them.");

            // Spend what is left of the window, and the next proposal waits for the next one.
            var engine = new EpisodeEngine(after);
            while (EpisodeEngine.SocialActionsSpent(engine.Snapshot) < EpisodeEngine.SocialActionBudget(engine.Snapshot))
                Assert.That(Apply(engine, EpisodeCommandKind.SmallTalk, Npcs(s)[1].id).accepted, Is.True);
            var spent = Apply(engine, EpisodeCommandKind.FormAlliance, asked.id);
            Assert.That(spent.accepted, Is.False, "With nothing left, nothing is put.");
            Assert.That(spent.reason, Is.EqualTo("This social window is complete. Continue the episode."));
        }

        /// <summary>
        /// A no is worded from what the player knows. Two houseguests who see the player very
        /// differently, but whom the player reads alike, turn them down in the same words under the
        /// rules; without them the words came from each one's hidden view of the player. A track
        /// record is the player's own breaches under the rules - not the houseguest's private record.
        /// </summary>
        [Test]
        public void UnderTheRulesANoIsWordedFromWhatThePlayerKnows()
        {
            foreach (bool rules in new[] { false, true })
            {
                var s = Season(67);
                if (rules) EpisodeEngine.EnableCommitments(s);
                var warm = Npcs(s)[0];
                var cold = Npcs(s)[1];
                Set(s, s.playerId, warm.id, 30); Set(s, s.playerId, cold.id, 30);
                Set(s, warm.id, s.playerId, 90); Set(s, cold.id, s.playerId, -90);
                string toWarm = PlayerDeals.Reasoning(s, warm.id, DealKind.AllianceInvite, false);
                string toCold = PlayerDeals.Reasoning(s, cold.id, DealKind.AllianceInvite, false);
                if (rules)
                {
                    Assert.That(toCold, Is.EqualTo(toWarm), "Under the rules the player hears the same no from both: they know them alike.");
                    Assert.That(PlayerDeals.Reasoning(s, cold.id, DealKind.AllianceInvite, true),
                        Is.EqualTo(PlayerDeals.Reasoning(s, warm.id, DealKind.AllianceInvite, true)), "and the same yes.");
                }
                else Assert.That(toCold, Is.Not.EqualTo(toWarm), "Without the rules the words read how each really sees the player.");
            }

            // The houseguest's private record of the player is theirs: under the rules it says nothing;
            // the player's own broken word does.
            var t = Rules(Season(68));
            var npc = Npcs(t)[0];
            Set(t, t.playerId, npc.id, 30); Set(t, npc.id, t.playerId, 30);
            RelationshipLedger.RecordOneWay(t, npc.id, t.playerId, "story:sold-out", -60, "Something they will not forget.");
            Assert.That(ThreatAssessment.TrustScore(t, t.playerId, npc.id), Is.LessThan(40), "Their private record of the player is poor.");
            Assert.That(PlayerDeals.Reasoning(t, npc.id, DealKind.AllianceInvite, false), Is.EqualTo("I'm not sure this is the right move for me."),
                "Under the rules that is not the player's to hear of.");
            t.promises.Add(new PromiseState
            {
                id = "promise-broken", fromId = t.playerId, toId = Npcs(t)[1].id, kind = PromiseKind.Safety, status = PromiseStatus.Broken,
                week = 1, expiresWeek = 2, brokenById = t.playerId, settledWeek = 1,
            });
            Assert.That(PlayerDeals.Reasoning(t, npc.id, DealKind.AllianceInvite, false), Is.EqualTo("Your track record concerns me."),
                "A promise the player broke is theirs to know.");
        }

        // ------------------------------------------------------------ a grudge refuses

        /// <summary>
        /// A houseguest who holds forty or more against the player says no to a pact whatever the
        /// roll - the proposal and the deal table's invitation alike - and the roll is drawn all the
        /// same, so a proposal is always one draw. Thirty-nine, and the same roll says yes. Without
        /// the rules a grudge was no gate: a view of eight or more was a pact.
        /// </summary>
        [Test]
        public void UnderTheRulesAGrudgeOfFortyOrMoreRefusesWhateverTheRoll()
        {
            foreach (double grudge in new[] { 39.99, 40 })
            {
                var s = Rules(Season(64));
                EpisodeEngine.EnableStory(s);
                var npc = Plain(Npcs(s)[0]);
                Set(s, npc.id, s.playerId, 100); Set(s, s.playerId, npc.id, 100);
                Assert.That(Grudges.Add(s, npc.id, s.playerId, grudge, GrudgeCauses.Story), Is.Not.Null);
                double chance = PlayerDeals.AcceptanceChance(s, npc.id, DealKind.AllianceInvite, null);
                Assert.That(chance, Is.GreaterThan(80), "The odds are long in the player's favour.");
                s.randomState = Draw(s, true, chance);
                var stream = new SeededRandom(s.randomState);
                stream.NextDouble();

                var result = Apply(new EpisodeEngine(s), EpisodeCommandKind.FormAlliance, npc.id);
                Assert.That(result.accepted, Is.True, result.reason);
                var after = result.state;
                bool refused = grudge >= GrudgeLine;
                Assert.That(after.Allied(s.playerId, npc.id), Is.EqualTo(!refused), grudge + " held against the player.");
                if (refused)
                {
                    Assert.That(after.randomState, Is.EqualTo(stream.State), "The roll was drawn, once, and said yes; the grudge said no.");
                    Assert.That(after.events.Last().text, Does.StartWith(npc.name + " turned down your alliance."));
                    Assert.That(EpisodeEngine.SocialActionsSpent(after), Is.EqualTo(EpisodeEngine.SocialActionsSpent(s) + 1), "and the action is spent.");
                }

                // The deal table's invitation is the same question, with the same grudge - and its own
                // roll is drawn all the same: the deal's draw, then the two of the warmth it moves
                // either way, yes or no.
                var dealStream = new SeededRandom(s.randomState);
                for (int draw = 0; draw < 3; draw++) dealStream.NextDouble();
                var deal = Apply(new EpisodeEngine(s), EpisodeCommandKind.ProposeDeal, npc.id, null, DealKind.AllianceInvite);
                Assert.That(deal.accepted, Is.True, deal.reason);
                Assert.That(deal.state.deals.Any(d => d.type == DealKind.AllianceInvite && d.proposerId == s.playerId), Is.EqualTo(!refused),
                    "The invitation, on the same roll: " + grudge + " held against the player.");
                Assert.That(deal.state.Allied(s.playerId, npc.id), Is.EqualTo(!refused));
                Assert.That(deal.state.randomState, Is.EqualTo(dealStream.State), "The invitation's roll is drawn whatever the grudge says.");
            }

            var free = Season(64);
            EpisodeEngine.EnableStory(free);
            var them = Plain(Npcs(free)[0]);
            Set(free, them.id, free.playerId, 100);
            Grudges.Add(free, them.id, free.playerId, 80, GrudgeCauses.Story);
            var formed = Apply(new EpisodeEngine(free), EpisodeCommandKind.FormAlliance, them.id);
            Assert.That(formed.accepted && formed.state.Allied(free.playerId, them.id), Is.True, "Without the rules the grudge was no gate.");
        }

        // ------------------------------------------------------------ three at once

        /// <summary>
        /// Decision 10: the player holds at most three pacts. Under the rules a fourth is refused
        /// before anybody is asked, and the refusal says why - by the proposal (no action, no roll),
        /// the deal table's invitation (not offered, with the reason), the yes to an invitation put to
        /// the player (the no is still theirs) and a story's pact. Leave one, and the fourth can be
        /// asked. Without the rules there was no cap.
        /// </summary>
        [Test]
        public void UnderTheRulesThePlayerHoldsAtMostThreePacts()
        {
            var free = Season(65);
            var fourth = Npcs(free)[3];
            Pacts(free, 3);
            Set(free, fourth.id, free.playerId, 50);
            var unbound = Apply(new EpisodeEngine(free), EpisodeCommandKind.FormAlliance, fourth.id);
            Assert.That(unbound.accepted, Is.True, unbound.reason);
            Assert.That(Held(unbound.state), Is.EqualTo(4), "Without the rules a fourth pact was formed like any other.");

            var s = Rules(Season(65));
            var pacts = Pacts(s, 3);
            Set(s, fourth.id, s.playerId, 50);
            Assert.That(Held(s), Is.EqualTo(Cap));
            var refused = Apply(new EpisodeEngine(s), EpisodeCommandKind.FormAlliance, fourth.id);
            Assert.That(refused.accepted, Is.False, "A fourth proposal is refused.");
            Assert.That(refused.reason, Is.EqualTo(CapRefusal), "and says why.");
            Assert.That((refused.state.revision, refused.state.randomState), Is.EqualTo((s.revision, s.randomState)),
                "Before anybody is asked: no action, no roll - the player can count their own pacts.");
            Assert.That(Refusal(s, fourth.id), Is.EqualTo(CapRefusal), "The row says so before it is pressed.");
            Assert.That(Refusal(s, Npcs(s)[0].id), Is.EqualTo("You already share an active alliance."), "A pact they share is refused as it always was.");

            Assert.That(PlayerDeals.CanPropose(s, fourth.id, DealKind.AllianceInvite, null, out string why), Is.False,
                "The deal table does not offer the invitation,");
            Assert.That(why, Is.EqualTo(CapRefusal), "and says why.");
            Assert.That(PlayerDeals.Available(s, fourth.id), Does.Not.Contain(DealKind.AllianceInvite));

            s.deals.Add(new DealState
            {
                id = "deal-ask-cap", type = DealKind.AllianceInvite, proposerId = fourth.id, recipientId = s.playerId,
                status = DealStatus.Proposed, week = s.week, expiresWeek = s.week, trustImpact = DealKind.DefaultTrust(DealKind.AllianceInvite),
            });
            var yes = Apply(new EpisodeEngine(s), EpisodeCommandKind.RespondToDeal, "deal-ask-cap", null, EpisodeEngine.AcceptDeal);
            Assert.That(yes.accepted, Is.False, "A yes to an invitation that would make a fourth is refused,");
            Assert.That(yes.reason, Is.EqualTo(CapRefusal));
            var no = Apply(new EpisodeEngine(s), EpisodeCommandKind.RespondToDeal, "deal-ask-cap", null, "decline");
            Assert.That(no.accepted, Is.True, "and the no is still the player's: " + no.reason);

            int before = s.alliances.Count;
            StoryAlliance(s, s.playerId, fourth.id);
            Assert.That(s.alliances.Count, Is.EqualTo(before), "A story brings the player into no fourth pact.");

            StoryAlliance(s, s.playerId, fourth.id, Npcs(s)[4].id);
            Assert.That(s.alliances.Count, Is.EqualTo(before), "nor into a fourth of three.");

            // Without the strategy windows an agreed invitation is a deal and never a pact, so the yes
            // is the player's to give at three as it always was.
            var windowless = s.Clone();
            windowless.strategyRulesStartWeek = 0;
            var dealOnly = Apply(new EpisodeEngine(windowless), EpisodeCommandKind.RespondToDeal, "deal-ask-cap", null, EpisodeEngine.AcceptDeal);
            Assert.That(dealOnly.accepted, Is.True, "A yes that makes no pact is allowed at three: " + dealOnly.reason);
            Assert.That(dealOnly.state.deals.Single(d => d.id == "deal-ask-cap").status, Is.EqualTo(DealStatus.Active));
            Assert.That(Held(dealOnly.state), Is.EqualTo(Cap), "and the player still holds three.");

            pacts[0].active = false;
            Assert.That(Refusal(s, fourth.id), Is.Null, "Leave one, and the fourth can be asked.");
            var asked = Apply(new EpisodeEngine(s), EpisodeCommandKind.FormAlliance, fourth.id);
            Assert.That(asked.accepted, Is.True, asked.reason);
            Set(free, Npcs(free)[4].id, free.playerId, 50);
            StoryAlliance(free, free.playerId, Npcs(free)[4].id);
            Assert.That(Held(free), Is.EqualTo(4), "Without the rules a story's pact was not held to three either,");
            StoryAlliance(free, free.playerId, Npcs(free)[5].id, Npcs(free)[6].id);
            Assert.That(Held(free), Is.EqualTo(5), "nor one of three.");
        }

        /// <summary>
        /// Nobody puts an invitation to a player who holds three pacts under the rules: a houseguest
        /// whose partnership and safety pact with the player have grown warm asks for something else,
        /// or nothing, where a yes would be refused. Below three, and without the rules, they ask.
        /// </summary>
        [Test]
        public void UnderTheRulesNobodyInvitesAPlayerWhoHoldsThree()
        {
            foreach (bool rules in new[] { false, true })
            foreach (int held in new[] { 2, 3 })
            {
                var s = Season(72);
                if (rules) EpisodeEngine.EnableCommitments(s);
                var npcs = Npcs(s);
                var asking = npcs[6];
                Pacts(s, held);
                Set(s, asking.id, s.playerId, 60);
                foreach (string kind in new[] { DealKind.Partnership, DealKind.SafetyAgreement })
                    s.deals.Add(new DealState
                    {
                        id = "deal-" + kind, type = kind, proposerId = asking.id, recipientId = s.playerId, status = DealStatus.Active,
                        week = s.week, expiresWeek = 0, trustImpact = DealKind.DefaultTrust(kind),
                    });
                string offer = NpcDeals.Offer(s, asking.id, s.playerId);
                string where = (rules ? "Under the rules" : "Without them") + ", holding " + held;
                if (rules && held == Cap) Assert.That(offer, Is.Not.EqualTo(DealKind.AllianceInvite), where + ": no invitation the player could only turn down.");
                else Assert.That(offer, Is.EqualTo(DealKind.AllianceInvite), where + ": partners, safe and warm, make it an alliance.");
            }
        }

        // ------------------------------------------------------------ a pact is on the record

        /// <summary>
        /// A pact the player forms under the rules writes the ledger's permanent 'alliance-formed',
        /// both ways and at thirty, as a pact between houseguests does, so twenty weeks on the partner
        /// still trusts the player exactly as a houseguest trusts their own pact-mate. The invitation's
        /// pact is on the record too, and joining a pact of theirs puts the player on it with every
        /// member. Without the rules a player's pact was never on the record.
        /// </summary>
        [Test]
        public void UnderTheRulesAPlayersPactBuildsTrustAsAPactBetweenHouseguestsDoes()
        {
            var house = Season(66);
            var pair = NpcAlliances.FormFromStory(house, new List<string> { Npcs(house)[4].id, Npcs(house)[5].id });
            Assert.That(pair, Is.Not.Null);
            var theirs = Entries(house, pair.members[0], pair.members[1], "alliance-formed").Single();
            house.week += 20;
            double houseTrust = ThreatAssessment.TrustScore(house, pair.members[0], pair.members[1]);
            Assert.That(houseTrust, Is.GreaterThan(ThreatAssessment.NeutralTrust));

            foreach (bool rules in new[] { false, true })
            {
                var s = Season(66);
                if (rules) EpisodeEngine.EnableCommitments(s);
                var npc = Plain(Npcs(s)[0]);
                Set(s, npc.id, s.playerId, 100); Set(s, s.playerId, npc.id, 100);
                s.randomState = Draw(s, true, PlayerDeals.AcceptanceChance(s, npc.id, DealKind.AllianceInvite, null));
                var result = Apply(new EpisodeEngine(s), EpisodeCommandKind.FormAlliance, npc.id);
                Assert.That(result.accepted, Is.True, result.reason);
                var after = result.state;
                Assert.That(after.Allied(s.playerId, npc.id), Is.True);
                var heldByThem = Entries(after, npc.id, s.playerId, "alliance-formed");
                var heldByPlayer = Entries(after, s.playerId, npc.id, "alliance-formed");
                if (!rules)
                {
                    Assert.That(heldByThem.Concat(heldByPlayer), Is.Empty, "Without the rules a player's pact was never on the record.");
                    continue;
                }
                foreach (var entry in new[] { heldByThem.Single(), heldByPlayer.Single() })
                {
                    Assert.That((entry.impactScore, entry.decayable), Is.EqualTo((theirs.impactScore, theirs.decayable)),
                        "The same entry a pact between houseguests writes: thirty, and it never fades.");
                }
                after.week += 20;
                Assert.That(ThreatAssessment.TrustScore(after, s.playerId, npc.id), Is.EqualTo(houseTrust).Within(1e-9),
                    "Twenty weeks on they trust the player as a houseguest trusts their own pact-mate.");
            }

            // The invitation's pact, and a pact of theirs the player is brought into.
            var t = Rules(Season(69));
            var npcs = Npcs(t);
            Invite(t, npcs[0].id);
            Assert.That(t.Allied(t.playerId, npcs[0].id), Is.True);
            Assert.That(Entries(t, npcs[0].id, t.playerId, "alliance-formed"), Has.Count.EqualTo(1), "An invitation's pact is on the record.");
            var join = Rules(Season(70));
            var others = Npcs(join);
            var bloc = NpcAlliances.FormFromStory(join, new List<string> { others[1].id, others[2].id });
            // A pact of three or more keeps a member who has gone (X5): one is evicted already.
            bloc.members.Add(others[3].id);
            others[3].status = ContestantStatus.Evicted;
            foreach (string member in bloc.members) { Set(join, member, join.playerId, 30); Set(join, join.playerId, member, 30); }
            Invite(join, others[1].id);
            Assert.That(bloc.members, Does.Contain(join.playerId), "Brought into their pact.");
            foreach (string member in new[] { others[1].id, others[2].id })
                Assert.That(Entries(join, member, join.playerId, "alliance-formed"), Has.Count.EqualTo(1),
                    "On the record with every member in the house: " + join.Find(member).name);
            Assert.That(Entries(join, others[3].id, join.playerId, "alliance-formed"), Is.Empty,
                "and with nobody who has left it.");
        }

        // ------------------------------------------------------------ the grudge the player can know of

        /// <summary>
        /// The one grudge the player can know of is the one their own walk-out left. Under the rules
        /// every member of a pact the player walked out of reads 'no chance', nought, while that
        /// grudge - what leaving leaves, less its fade since - is at the line, and a proposal to them
        /// is turned down, the action spent: the read and the answer agree. Somebody the player never
        /// walked out on reads as before, and twenty-one weeks on the reckoning is under the line.
        /// Without the rules a walk-out was no gate, and the read never says it is one.
        /// </summary>
        [Test]
        public void UnderTheRulesTheShownChanceKnowsTheGrudgeThePlayersOwnWalkOutLeft()
        {
            foreach (bool rules in new[] { false, true })
            {
                var s = Season(73);
                if (rules) EpisodeEngine.EnableCommitments(s);
                EpisodeEngine.EnableStory(s);
                var npcs = Npcs(s);
                var told = Plain(npcs[0]);
                var other = Plain(npcs[1]);
                var stranger = Plain(npcs[2]);
                foreach (var npc in new[] { told, other, stranger }) { Set(s, npc.id, s.playerId, 100); Set(s, s.playerId, npc.id, 100); }
                s.alliances.Add(new AllianceState
                {
                    id = "alliance-left", name = "The Left Pact", members = new List<string> { s.playerId, told.id, other.id }, active = true,
                });
                var engine = new EpisodeEngine(s);
                // A command first, so the pact has its ledger row, as a pact the player is in always has.
                Assert.That(Apply(engine, EpisodeCommandKind.SmallTalk, stranger.id).accepted, Is.True);
                var walkOut = Apply(engine, EpisodeCommandKind.LeaveAlliance, told.id);
                Assert.That(walkOut.accepted, Is.True, walkOut.reason);
                var after = engine.Snapshot;
                Assert.That(Grudges.Severity(after, told.id, after.playerId), Is.GreaterThanOrEqualTo(GrudgeLine), "Walking out left a grudge.");
                Assert.That(Grudges.Severity(after, other.id, after.playerId), Is.GreaterThanOrEqualTo(GrudgeLine), "with every member.");
                string where = rules ? "Under the rules" : "Without them";
                if (!rules)
                {
                    Assert.That(Shown(after, told.id).word, Is.Not.EqualTo(NoChance), where + " a walk-out is never read as a no.");
                    continue;
                }
                foreach (var member in new[] { told, other })
                {
                    var shown = Shown(after, member.id);
                    Assert.That((shown.word, shown.chance), Is.EqualTo((NoChance, 0.0)),
                        where + ", " + member.name + " holds what the player's own walk-out left, and the read says so.");
                }
                Assert.That(Shown(after, stranger.id).word, Is.Not.EqualTo(NoChance), where + ", somebody the player never walked out on reads as before.");

                var asked = Apply(engine, EpisodeCommandKind.FormAlliance, told.id);
                Assert.That(asked.accepted, Is.True, asked.reason);
                Assert.That(asked.state.Allied(s.playerId, told.id), Is.False, "and the proposal is turned down, as the read said.");
                Assert.That(asked.state.events.Last().kind, Is.EqualTo(RefusedKind));

                var later = after.Clone();
                later.week += 21;
                Assert.That(Shown(later, told.id).word, Is.Not.EqualTo(NoChance), "Twenty-one weeks on, the reckoning is under the line.");
            }
        }

        // ------------------------------------------------------------ what a houseguest says when you open a conversation

        /// <summary>
        /// The knowledge gate on a greeting. Under the rules how a houseguest greets the player reads
        /// only what the player knows of where they stand - the player's own reading of them, inside
        /// the band a read put them in - never how they privately see the player, which the greeting
        /// said aloud every time a conversation opened. Without the rules it is unchanged.
        /// </summary>
        [Test]
        public void UnderTheRulesAGreetingSaysNothingOfTheirHiddenView()
        {
            const string maya = ContentCatalog.MayaId;
            foreach (bool rules in new[] { false, true })
            {
                var state = ContentCatalog.Create(412);
                if (rules) EpisodeEngine.EnableCommitments(state);
                Set(state, state.playerId, maya, 0);
                Set(state, maya, state.playerId, 60);
                string warm = HouseDialogue.Greeting(state, maya);
                Set(state, maya, state.playerId, -60);
                string cold = HouseDialogue.Greeting(state, maya);
                if (rules) Assert.That(cold, Is.EqualTo(warm), "Under the rules how Maya privately sees the player is not said.");
                else Assert.That(cold, Is.Not.EqualTo(warm), "Without the rules the greeting said it.");
            }

            var known = ContentCatalog.Create(412);
            EpisodeEngine.EnableCommitments(known);
            Set(known, known.playerId, maya, 0);
            Set(known, maya, known.playerId, 60);
            string unread = HouseDialogue.Greeting(known, maya);
            known.ledger.standings.Add(new StandingRow { week = known.week, fromId = maya, toId = known.playerId, source = ClaimSource.Read, score = -60 });
            Assert.That(HouseDialogue.Greeting(known, maya), Is.Not.EqualTo(unread),
                "A read that found her cold on the player is the player's to know, and the greeting goes with it.");
        }

        // ------------------------------------------------------------ without the rules

        /// <summary>
        /// A season without the commitment rules - every recorded season, and every season a test
        /// builds directly - proposes as it always did: a view of eight or more is a pact whatever the
        /// roll, with one draw for the warmth's reciprocal and nothing on the record; under eight is
        /// refused for nothing. (Tools/SimulationTests' CommitmentRulesSeasonDigests holds 54 such
        /// seasons byte for byte.)
        /// </summary>
        [Test]
        public void WithoutTheRulesTheProposalPlaysAsItAlwaysDid()
        {
            foreach (uint draw in new uint[] { 1, 2, 3, 4, 5, 6 })
            {
                var s = Season(71);
                var npc = Npcs(s)[0];
                Set(s, npc.id, s.playerId, 8);
                s.randomState = draw * 2654435761u;
                var stream = new SeededRandom(s.randomState);
                stream.NextDouble();
                var result = Apply(new EpisodeEngine(s), EpisodeCommandKind.FormAlliance, npc.id);
                Assert.That(result.accepted, Is.True, result.reason);
                var after = result.state;
                Assert.That(after.Allied(s.playerId, npc.id), Is.True, "Eight is a pact, whatever the roll.");
                Assert.That(after.randomState, Is.EqualTo(stream.State), "One draw: the warmth's reciprocal.");
                Assert.That(after.events.Last().text, Is.EqualTo("You and " + npc.name + " formed a private alliance."));
                Assert.That(after.relationships.SelectMany(r => r.events).Any(e => e.type == "alliance-formed"), Is.False, "Nothing on the record.");
                Assert.That(EpisodeEngine.SocialActionsSpent(after), Is.EqualTo(EpisodeEngine.SocialActionsSpent(s) + 1));
            }
            var refused = Season(71);
            var cold = Npcs(refused)[0];
            Set(refused, cold.id, refused.playerId, 7.99);
            var probe = Apply(new EpisodeEngine(refused), EpisodeCommandKind.FormAlliance, cold.id);
            Assert.That(probe.accepted, Is.False, "Under eight is refused,");
            Assert.That(probe.state.randomState, Is.EqualTo(refused.randomState), "for nothing.");
        }

        /// <summary>
        /// What the houseguest says after a proposal: under the rules a committed proposal that left no
        /// pact is a no, said without a reason; a yes is answered as it always was; without the rules a
        /// committed proposal always left a pact, and its answer is unchanged.
        /// </summary>
        [Test]
        public void UnderTheRulesAHouseguestWhoTurnsThePlayerDownSaysNo()
        {
            var state = ContentCatalog.Create(412);
            const string maya = ContentCatalog.MayaId;
            string plain = HouseDialogue.Response(state, maya);
            Assert.That(HouseDialogue.Response(state, maya, EpisodeCommandKind.FormAlliance), Is.EqualTo(plain),
                "Without the rules a proposal never left them apart, and nothing new is said.");
            EpisodeEngine.EnableCommitments(state);
            Assert.That(HouseDialogue.Response(state, maya, EpisodeCommandKind.FormAlliance),
                Is.EqualTo("Not an alliance, not yet. I'd rather we earn one than announce it."), "Under the rules it is a no.");
            state.alliances.Add(new AllianceState { id = "alliance-maya", name = "The Maya Pact", members = new List<string> { state.playerId, maya } });
            Assert.That(HouseDialogue.Response(state, maya, EpisodeCommandKind.FormAlliance), Does.StartWith("We have an alliance"),
                "and a yes is answered as it always was.");
        }

        // ------------------------------------------------------------ the slice's own API
        //
        // Every name C4 added is read through these, and only these, so the file compiles against
        // the build before it (50ebd57) with their bodies stubbed to that build's behaviour - which
        // is how each test above was seen to fail there.

        /// <summary>The chance the row shows: the player's read of the invitation's (KnownOdds.Alliance).</summary>
        private static KnownOdds.Estimate Shown(EpisodeState s, string npcId) => KnownOdds.Alliance(s, npcId);

        /// <summary>Why the row is locked, or null (EpisodeEngine.AllianceRefusal).</summary>
        private static string Refusal(EpisodeState s, string npcId) => EpisodeEngine.AllianceRefusal(s, npcId);

        private static string CapRefusal => EpisodeEngine.PactCapRefusal;

        private static int Cap => EpisodeEngine.PlayerPactCap;

        private static double GrudgeLine => EpisodeEngine.AllianceGrudgeLine;

        private static string RefusedKind => EpisodeEngine.AllianceRefusedKind;

        private static string NoChance => KnownOdds.NoChance;

        // ------------------------------------------------------------ fixtures

        private static EpisodeState Season(uint seed, int size = 8) =>
            SeasonBuilder.Create(new SeasonBuilder.Choice { HouseSize = size }, seed);

        private static EpisodeState Rules(EpisodeState s)
        {
            EpisodeEngine.EnableCommitments(s);
            return s;
        }

        private static List<ContestantState> Npcs(EpisodeState s) => s.Active.Where(c => !c.isPlayer).ToList();

        /// <summary>No traits, so a houseguest's own lines on the roll stay out of the way.</summary>
        private static ContestantState Plain(ContestantState npc)
        {
            npc.traits = new List<string>();
            return npc;
        }

        private static void Set(EpisodeState s, string from, string to, double score)
        {
            var edge = s.relationships.FirstOrDefault(r => r.fromId == from && r.toId == to);
            if (edge == null) s.relationships.Add(edge = new RelationshipState { fromId = from, toId = to });
            edge.score = score;
        }

        /// <summary>
        /// A houseguest the player knows every term about: they see the player as the player sees
        /// them, and nothing on their private record is about the player, so its trust is neutral.
        /// </summary>
        private static EpisodeState KnownAlike(EpisodeState s, int index, double view, out ContestantState npc)
        {
            npc = Npcs(s)[index];
            Set(s, npc.id, s.playerId, view);
            Set(s, s.playerId, npc.id, view);
            Assert.That(ThreatAssessment.TrustScore(s, s.playerId, npc.id), Is.EqualTo(ThreatAssessment.NeutralTrust),
                "Nothing on their private record is about the player.");
            Assert.That(KnownOdds.PresumedView(s, npc.id), Is.EqualTo(view), "and how they see the player is the player's own reading.");
            return s;
        }

        /// <summary>A random state whose next draw says yes, or no, to a chance.</summary>
        private static uint Draw(EpisodeState s, bool yes, double chance)
        {
            for (uint n = 1; n < 10000; n++)
            {
                uint state = n * 2654435761u;
                if ((new SeededRandom(state).NextDouble() * 100 < chance) == yes) return state;
            }
            Assert.Fail("No draw says " + (yes ? "yes" : "no") + " to " + chance + ".");
            return 0;
        }

        /// <summary>Two-person pacts between the player and the first <paramref name="count"/> houseguests.</summary>
        private static List<AllianceState> Pacts(EpisodeState s, int count)
        {
            var pacts = Npcs(s).Take(count).Select(npc => new AllianceState
            {
                id = "alliance-held-" + npc.id, name = "The " + npc.name.Split(' ')[0] + " Pact",
                members = new List<string> { s.playerId, npc.id }, active = true,
            }).ToList();
            s.alliances.AddRange(pacts);
            return pacts;
        }

        private static int Held(EpisodeState s) => s.alliances.Count(a => a.active && a.members.Contains(s.playerId));

        private static List<RelationshipEventState> Entries(EpisodeState s, string from, string to, string type) =>
            s.relationships.Where(r => r.fromId == from && r.toId == to).SelectMany(r => r.events).Where(e => e.type == type).ToList();

        private static EpisodeCommand Command(EpisodeState s, EpisodeCommandKind kind, string target = null, string second = null, string text = null) =>
            new EpisodeCommand
            {
                id = "alliance-proposal-" + kind + "-" + s.revision, actorId = s.playerId, kind = kind,
                targetId = target, secondTargetId = second, text = text, expectedRevision = s.revision, expectedPhase = s.phase,
            };

        private static CommandResult Apply(EpisodeEngine engine, EpisodeCommandKind kind, string target = null, string second = null, string text = null) =>
            engine.Apply(Command(engine.Snapshot, kind, target, second, text));

        /// <summary>An invitation agreed, as the engine writes its pact (the private AllyThroughInvitation).</summary>
        private static void Invite(EpisodeState s, string npcId) =>
            typeof(EpisodeEngine).GetMethod("AllyThroughInvitation", BindingFlags.NonPublic | BindingFlags.Static)
                .Invoke(null, new object[] { s, npcId });

        /// <summary>A story's pact of two, or of three with <paramref name="c"/> (the private StoryAlliance).</summary>
        private static void StoryAlliance(EpisodeState s, string a, string b, string c = null) =>
            typeof(EpisodeEngine).GetMethod("StoryAlliance", BindingFlags.NonPublic | BindingFlags.Static)
                .Invoke(null, new object[] { s, s.Find(a), s.Find(b), c == null ? null : s.Find(c), "c4-story-pact" });
    }
}
