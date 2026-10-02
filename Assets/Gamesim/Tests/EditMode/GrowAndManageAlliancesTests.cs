using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Gamesim.Simulation;
using Newtonsoft.Json;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// Grow and manage alliances (ACTIONS-DEALS-ALLIANCES-PLAN C5), under the commitment rules: "Bring
    /// {name} into {pact}" asks the houseguest on the alliance invitation's odds, one draw, and every
    /// member in the house has their say through <c>NpcAlliances.WouldWelcome</c> - WouldPropose's
    /// question asked of a pact that exists - one no refusing it and the action spent either way; a
    /// newcomer joins at the end, so the founder, <c>members[0]</c>, never changes, and the founder calls
    /// the bloc's vote; "Rename {pact}" is the founder's, from the names on offer, free and once a week;
    /// and "Leave {pact}" names its pact and, in a pact of three or more, takes only the player out at
    /// the walk-out's price. Without the rules the new kinds are refused before anything moves, and a
    /// leave is what it always was. Unity-free, so the dotnet subset runs it (Tools/SimulationTests). The
    /// screen's half is EpisodePlayModeTests.GrowAndManageAlliances.
    /// </summary>
    public sealed class GrowAndManageAlliancesTests
    {
        // ------------------------------------------------------------ bring {name} into {pact}: the members' say

        /// <summary>
        /// Every member still in the house has their say, each by WouldWelcome and nothing else: both
        /// welcome the newcomer, and the newcomer who says yes is in; one of them does not, and nobody is
        /// added. Either way the action is spent and the season's stream gives one draw to the asking
        /// (and, on a yes, the reciprocal of the warmth a pact with the player brings). A no says who
        /// said it, to the player's face, and moves nobody.
        /// </summary>
        [Test]
        public void UnderTheRulesEveryMemberHasTheirSayAndOneNoRefuses()
        {
            foreach (int refuser in new[] { -1, 0, 1 })
            {
                var s = Rules(Season(81));
                var npcs = Npcs(s);
                var members = new[] { Plain(npcs[0]), Plain(npcs[1]) };
                var maya = Plain(npcs[2]);
                var pact = Pact(s, "alliance-grow", "The Grow Pact", "player", s.playerId, members[0].id, members[1].id);
                Warm(s, maya.id, s.playerId);
                // The player's own view of her has room to warm.
                Set(s, s.playerId, maya.id, 30);
                foreach (var member in members) Set(s, member.id, maya.id, 100);
                if (refuser >= 0) Set(s, members[refuser].id, maya.id, 10);
                string where = refuser < 0 ? "Both welcome Maya" : members[refuser].name + " does not";
                for (int i = 0; i < members.Length; i++)
                    Assert.That(Welcome(s, members[i].id, maya.id), Is.EqualTo(i != refuser), where + ": each member's say is theirs.");

                s.randomState = Draw(s, true, PlayerDeals.AcceptanceChance(s, maya.id, DealKind.AllianceInvite, null));
                var stream = new SeededRandom(s.randomState);
                stream.NextDouble();
                int spent = EpisodeEngine.SocialActionsSpent(s);
                var result = Apply(new EpisodeEngine(s), BringIn, maya.id, pact.id);
                Assert.That(result.accepted, Is.True, where + ": the asking is put, whatever the answer: " + result.reason);
                var after = result.state;
                Assert.That(EpisodeEngine.SocialActionsSpent(after), Is.EqualTo(spent + 1), where + ": the action is spent either way.");
                var grown = after.alliances.Single(a => a.id == pact.id);
                if (refuser < 0)
                {
                    Assert.That(grown.members, Is.EqualTo(new[] { s.playerId, members[0].id, members[1].id, maya.id }), where + ": she is in.");
                    stream.NextDouble();
                    Assert.That(after.randomState, Is.EqualTo(stream.State), "The asking's draw, and the warmth's reciprocal.");
                    Assert.That(after.Score(s.playerId, maya.id), Is.GreaterThan(s.Score(s.playerId, maya.id)), "A pact with the player brings its warmth.");
                    var line = after.events.Last();
                    Assert.That(line.kind, Is.EqualTo("alliance"));
                    Assert.That(line.text, Is.EqualTo(JoinedLine(maya.name, "The Grow Pact")));
                    Assert.That(line.audienceIds, Is.EquivalentTo(new[] { s.playerId, members[0].id, members[1].id, maya.id }), "Everyone in it hears.");
                    continue;
                }
                Assert.That(grown.members, Is.EqualTo(new[] { s.playerId, members[0].id, members[1].id }), where + ": one no is enough.");
                Assert.That(after.randomState, Is.EqualTo(stream.State), where + ": one draw, hers, and nothing else.");
                Assert.That(Json(after.relationships), Is.EqualTo(Json(s.relationships)), where + ": nobody's view moves, and nothing is on the record.");
                var no = after.events.Last();
                Assert.That(no.kind, Is.EqualTo(RefusedKind), "Its own kind, as a turned-down proposal's.");
                Assert.That(no.text, Is.EqualTo(members[refuser].name + " won't have " + maya.name + " in The Grow Pact."), where + ": it says who said no.");
                Assert.That(no.audienceIds, Is.EquivalentTo(new[] { s.playerId, maya.id, members[refuser].id }), "Said to the player's face, and Maya hears it.");
            }
        }

        /// <summary>
        /// The houseguest asked answers as a proposal is answered (C4): one draw from the season's stream
        /// against the alliance invitation's chance, which - where the player knows every term the roll
        /// reads - is exactly the chance the row shows. A grudge of forty or more says no whatever the
        /// draw, the draw taken all the same. Their no is in their own words, the reason worded from what
        /// the player knows.
        /// </summary>
        [Test]
        public void UnderTheRulesTheOneAskedAnswersOnTheOddsTheRowShows()
        {
            int yes = 0, no = 0;
            foreach (double view in new[] { -40.0, 10, 45, 85 })
            for (uint draw = 1; draw <= 16; draw++)
            {
                var s = Rules(Season(82));
                var npcs = Npcs(s);
                var riley = Plain(npcs[0]);
                var maya = Plain(npcs[3]);
                var pact = Pact(s, "alliance-grow", "The Grow Pact", "player", s.playerId, riley.id);
                Set(s, riley.id, maya.id, 100);
                Set(s, maya.id, s.playerId, view); Set(s, s.playerId, maya.id, view);
                s.randomState = draw * 2654435761u;
                string where = "Maya at " + view + ", draw " + draw;
                var shown = KnownOdds.Alliance(s, maya.id);
                Assert.That(shown.chance, Is.EqualTo(PlayerDeals.AcceptanceChance(s, maya.id, DealKind.AllianceInvite, null)).Within(1e-9),
                    where + ": the player knows every term, so the row's chance is the roll's.");
                var stream = new SeededRandom(s.randomState);
                bool predicted = stream.NextDouble() * 100 < shown.chance;
                var result = Apply(new EpisodeEngine(s), BringIn, maya.id, pact.id);
                Assert.That(result.accepted, Is.True, where + ": " + result.reason);
                var after = result.state;
                Assert.That(after.alliances.Single(a => a.id == pact.id).members.Contains(maya.id), Is.EqualTo(predicted), where + ": the draw against the chance shown.");
                if (predicted) stream.NextDouble();
                Assert.That(after.randomState, Is.EqualTo(stream.State), where + ": one draw for the asking.");
                if (!predicted)
                    Assert.That(after.events.Last().text, Is.EqualTo(maya.name + " turned down The Grow Pact. “"
                        + PlayerDeals.Reasoning(s, maya.id, DealKind.AllianceInvite, false) + "”"), where + ": in her own words.");
                if (predicted) yes++; else no++;
            }
            Assert.That(yes, Is.GreaterThan(0), "Some said yes.");
            Assert.That(no, Is.GreaterThan(0), "Some said no.");

            // A grudge of forty or more refuses whatever the draw; the draw is taken all the same.
            var g = Rules(Season(83));
            EpisodeEngine.EnableStory(g);
            var gnpcs = Npcs(g);
            var partner = Plain(gnpcs[0]);
            var held = Plain(gnpcs[3]);
            var gpact = Pact(g, "alliance-grudge", "The Grudge Pact", "player", g.playerId, partner.id);
            Set(g, partner.id, held.id, 100);
            Warm(g, held.id, g.playerId);
            Assert.That(Grudges.Add(g, held.id, g.playerId, 40, GrudgeCauses.Story), Is.Not.Null);
            g.randomState = Draw(g, true, PlayerDeals.AcceptanceChance(g, held.id, DealKind.AllianceInvite, null));
            var gstream = new SeededRandom(g.randomState);
            gstream.NextDouble();
            var refused = Apply(new EpisodeEngine(g), BringIn, held.id, gpact.id);
            Assert.That(refused.accepted, Is.True, refused.reason);
            Assert.That(refused.state.alliances.Single(a => a.id == gpact.id).members, Does.Not.Contain(held.id), "A grudge of forty refuses,");
            Assert.That(refused.state.randomState, Is.EqualTo(gstream.State), "and the draw is taken all the same.");
            Assert.That(refused.state.events.Last().text, Does.StartWith(held.name + " turned down The Grudge Pact."));
        }

        /// <summary>
        /// Both no: the one asked turned it down, in their words, and the member who would not have had
        /// them is named too - everyone who said no, to the player's face.
        /// </summary>
        [Test]
        public void UnderTheRulesANoFromBothSidesNamesBoth()
        {
            var s = Rules(Season(84));
            var npcs = Npcs(s);
            var riley = Plain(npcs[0]);
            var maya = Plain(npcs[3]);
            var pact = Pact(s, "alliance-grow", "The Grow Pact", "player", s.playerId, riley.id);
            Set(s, riley.id, maya.id, 0);
            Set(s, maya.id, s.playerId, 0);
            s.randomState = Draw(s, false, PlayerDeals.AcceptanceChance(s, maya.id, DealKind.AllianceInvite, null));
            string said = PlayerDeals.Reasoning(s, maya.id, DealKind.AllianceInvite, false);
            var result = Apply(new EpisodeEngine(s), BringIn, maya.id, pact.id);
            Assert.That(result.accepted, Is.True, result.reason);
            var line = result.state.events.Last();
            Assert.That(line.text, Is.EqualTo(maya.name + " turned down The Grow Pact. “" + said + "” " + riley.name + " wouldn't have had "
                + maya.name.Split(' ')[0] + " in it either."));
            Assert.That(line.audienceIds, Is.EquivalentTo(new[] { s.playerId, maya.id, riley.id }));
        }

        /// <summary>
        /// WouldWelcome is WouldPropose's question asked of a pact that exists: the same floor, desire
        /// and grudge, and the newcomer's own three - nobody offers a pact to somebody who carries three.
        /// It leaves out what decides only whether a new pact may be made: that the two already share
        /// one, the member's own count (joining adds them no pact), and the cap on pacts among
        /// houseguests under agency, which a pact of the player's is outside.
        /// </summary>
        [Test]
        public void WelcomingIsProposingLessWhatDecidesOnlyANewPact()
        {
            // Two shared threats, so the desire clears its threshold however many pacts the member carries.
            EpisodeState Fixture(out ContestantState member, out ContestantState invitee)
            {
                var s = Rules(Season(85));
                var npcs = Npcs(s);
                member = Plain(npcs[0]);
                invitee = Plain(npcs[1]);
                Pact(s, "alliance-grow", "The Grow Pact", "player", s.playerId, member.id);
                Set(s, member.id, invitee.id, 100);
                foreach (var threat in new[] { npcs[5], npcs[6] })
                {
                    Set(s, member.id, threat.id, -60);
                    Set(s, invitee.id, threat.id, -60);
                }
                return s;
            }

            var plain = Fixture(out var m, out var i);
            Assert.That((Welcome(plain, m.id, i.id), NpcAlliances.WouldPropose(plain, m.id, i.id)), Is.EqualTo((true, true)), "Where a pact could be made, the two agree.");

            var shared = Fixture(out m, out i);
            shared.alliances.Add(new AllianceState { id = "alliance-npc-shared", name = "The Shared Pact", members = new List<string> { m.id, i.id } });
            Assert.That(NpcAlliances.WouldPropose(shared, m.id, i.id), Is.False, "A second pact between the two is never proposed,");
            Assert.That(Welcome(shared, m.id, i.id), Is.True, "but a partner is welcome all the more.");

            var busy = Fixture(out m, out i);
            var npcs = Npcs(busy);
            busy.alliances.Add(new AllianceState { id = "alliance-npc-busy-1", name = "Busy One", members = new List<string> { m.id, npcs[2].id } });
            busy.alliances.Add(new AllianceState { id = "alliance-npc-busy-2", name = "Busy Two", members = new List<string> { m.id, npcs[3].id } });
            Assert.That(NpcAlliances.ActiveAlliancesFor(busy, m.id), Has.Count.EqualTo(NpcAlliances.MaximumEach));
            Assert.That(NpcAlliances.WouldPropose(busy, m.id, i.id), Is.False, "A member who carries three proposes nothing,");
            Assert.That(Welcome(busy, m.id, i.id), Is.True, "but joining adds them no pact.");

            var agency = Fixture(out m, out i);
            EpisodeEngine.EnableAgency(agency);
            agency.alliances.Add(new AllianceState { id = "alliance-npc-theirs", name = "Theirs", members = new List<string> { i.id, Npcs(agency)[4].id } });
            Assert.That(NpcAlliances.PactRoom(agency, m.id, i.id), Is.False, "Under agency the house has no room for a new pact of theirs,");
            Assert.That(NpcAlliances.WouldPropose(agency, m.id, i.id), Is.False);
            Assert.That(Welcome(agency, m.id, i.id), Is.True, "and a pact of the player's is outside that cap.");

            var full = Fixture(out m, out i);
            var others = Npcs(full);
            for (int n = 0; n < NpcAlliances.MaximumEach; n++)
                full.alliances.Add(new AllianceState { id = "alliance-npc-full-" + n, name = "Full " + n, members = new List<string> { i.id, others[2 + n].id } });
            Assert.That((Welcome(full, m.id, i.id), NpcAlliances.WouldPropose(full, m.id, i.id)), Is.EqualTo((false, false)),
                "Somebody who carries three is offered no fourth, either way.");

            var cold = Fixture(out m, out i);
            Set(cold, m.id, i.id, 24);
            Assert.That((Welcome(cold, m.id, i.id), NpcAlliances.WouldPropose(cold, m.id, i.id)), Is.EqualTo((false, false)), "Under the floor, either way.");

            var grudging = Fixture(out m, out i);
            EpisodeEngine.EnableStory(grudging);
            Grudges.Add(grudging, i.id, m.id, 40, GrudgeCauses.Story);
            Assert.That((Welcome(grudging, m.id, i.id), NpcAlliances.WouldPropose(grudging, m.id, i.id)), Is.EqualTo((false, false)),
                "A grudge of forty either way bars it, either way.");
        }

        // ------------------------------------------------------------ what the player can see refuses for nothing

        /// <summary>
        /// What the player can see for themselves is refused before anybody is asked, spending nothing:
        /// a pact that is not theirs, somebody already in it, and a pact already holding four in the
        /// house. The departed (X5) do not count toward the four.
        /// </summary>
        [Test]
        public void UnderTheRulesWhatThePlayerCanSeeIsRefusedForNothing()
        {
            var s = Rules(Season(86));
            var npcs = Npcs(s);
            foreach (var npc in npcs) Warm(s, npc.id, s.playerId);
            var theirs = new AllianceState { id = "alliance-npc-theirs", name = "Their Pact", members = new List<string> { npcs[5].id, npcs[6].id } };
            s.alliances.Add(theirs);
            var four = Pact(s, "alliance-four", "The Four", "player", s.playerId, npcs[0].id, npcs[1].id, npcs[2].id);
            foreach (var (pactId, who, expected) in new[]
            {
                (theirs.id, npcs[3].id, NotYours),
                ("alliance-missing", npcs[3].id, NotYours),
                (four.id, npcs[1].id, npcs[1].name + " is already in The Four."),
                (four.id, npcs[3].id, "The Four already has " + Largest + " in the house, the most a pact holds."),
            })
            {
                var refused = Apply(new EpisodeEngine(s), BringIn, who, pactId);
                Assert.That(refused.accepted, Is.False, pactId + ", " + who);
                Assert.That(refused.reason, Is.EqualTo(expected));
                Assert.That((refused.state.revision, refused.state.randomState, EpisodeEngine.SocialActionsSpent(refused.state)),
                    Is.EqualTo((s.revision, s.randomState, EpisodeEngine.SocialActionsSpent(s))), "Nothing is spent: the player can see it.");
            }
            Assert.That(Largest, Is.EqualTo(4));

            // One of the four has left the house: three are in it, and a fourth can be asked.
            npcs[2].status = ContestantStatus.Evicted;
            Set(s, npcs[0].id, npcs[3].id, 100); Set(s, npcs[1].id, npcs[3].id, 100);
            Assert.That(Refusal(s, npcs[3].id, four), Is.Null, "The departed do not count toward the four.");
            s.randomState = Draw(s, true, PlayerDeals.AcceptanceChance(s, npcs[3].id, DealKind.AllianceInvite, null));
            var asked = Apply(new EpisodeEngine(s), BringIn, npcs[3].id, four.id);
            Assert.That(asked.accepted, Is.True, asked.reason);
            Assert.That(asked.state.alliances.Single(a => a.id == four.id).members.Last(), Is.EqualTo(npcs[3].id), "In, at the end.");
            Assert.That(Allegiance.Counted(asked.state, asked.state.alliances.Single(a => a.id == four.id)), Has.Count.EqualTo(Largest), "Four in the house now.");
        }

        // ------------------------------------------------------------ the founder

        /// <summary>
        /// The founder is the pact's first member and never changes when it grows: a pact the player made
        /// opens with them, one they were brought into keeps its founder first (the invitation appends
        /// the player), and whoever the player brings in joins at the end of either. Joining is on the
        /// record with every member still in the house, both ways, as a pact's founding is - and with
        /// nobody who has left it.
        /// </summary>
        [Test]
        public void UnderTheRulesTheFounderNeverChangesAndTheNewcomerIsOnTheRecordWithEveryMember()
        {
            var s = Rules(Season(87));
            var npcs = Npcs(s);
            var mine = Pact(s, "alliance-mine", "The Mine Pact", "player", s.playerId, npcs[0].id, npcs[4].id);
            npcs[4].status = ContestantStatus.Evicted;
            var bloc = NpcAlliances.FormFromStory(s, new List<string> { npcs[1].id, npcs[2].id });
            Assert.That(bloc, Is.Not.Null);
            string founder = bloc.members[0];
            foreach (string member in bloc.members) { Set(s, member, s.playerId, 30); Set(s, s.playerId, member, 30); }
            Invite(s, npcs[1].id);
            Assert.That(bloc.members, Is.EqualTo(new[] { npcs[1].id, npcs[2].id, s.playerId }), "Brought into their pact, the player joins at its end:");
            Assert.That(Founder(bloc), Is.EqualTo(founder), "its founder is still first.");
            Assert.That(Founder(mine), Is.EqualTo(s.playerId), "A pact the player made opens with them.");

            var maya = Plain(npcs[3]);
            Warm(s, maya.id, s.playerId);
            foreach (var npc in npcs.Where(npc => npc.id != maya.id)) Set(s, npc.id, maya.id, 100);
            var engine = new EpisodeEngine(s);
            foreach (var pact in new[] { mine, bloc })
            {
                var now = engine.Snapshot;
                now.randomState = Draw(now, true, PlayerDeals.AcceptanceChance(now, maya.id, DealKind.AllianceInvite, null));
                engine = new EpisodeEngine(now);
                var result = Apply(engine, BringIn, maya.id, pact.id);
                Assert.That(result.accepted, Is.True, result.reason);
                var grown = engine.Snapshot.alliances.Single(a => a.id == pact.id);
                Assert.That(grown.members.Last(), Is.EqualTo(maya.id), pact.name + ": Maya joins at the end,");
                Assert.That(Founder(grown), Is.EqualTo(Founder(pact)), "and the founder does not change.");
            }
            var after = engine.Snapshot;
            foreach (string member in new[] { s.playerId, npcs[0].id })
            {
                Assert.That(Entries(after, member, maya.id, "alliance-formed").Count(e => e.description == "The Mine Pact was joined"), Is.EqualTo(1),
                    "Joining is on the record with " + member + ",");
                Assert.That(Entries(after, maya.id, member, "alliance-formed").Count(e => e.description == "The Mine Pact was joined"), Is.EqualTo(1), "both ways,");
            }
            Assert.That(Entries(after, npcs[4].id, maya.id, "alliance-formed"), Is.Empty, "and with nobody who has left it.");
            Assert.That(Entries(after, npcs[1].id, maya.id, "alliance-formed").Single().decayable, Is.False, "It never fades.");
        }

        /// <summary>
        /// The bloc's caller reads the founder: under the rules a pact's first member calls its vote
        /// whenever they are among the members who vote - a houseguest's pact's founder, and the player
        /// in a pact they made - where without the rules the round picked whoever trusted the rest most,
        /// as the source does when it has no founder. A founder on the block leaves the call to that pick.
        /// </summary>
        [Test]
        public void UnderTheRulesTheBlocsCallerIsTheFounder()
        {
            foreach (bool rules in new[] { false, true })
            {
                var s = Season(88, 10);
                if (rules) EpisodeEngine.EnableCommitments(s);
                var npcs = Npcs(s);
                string founder = npcs[0].id, trusted = npcs[1].id, third = npcs[2].id;
                s.alliances.Add(new AllianceState { id = "alliance-npc-bloc", name = "The Bloc", members = new List<string> { founder, trusted, third } });
                s.alliances.Add(new AllianceState { id = "alliance-mine", name = "The Mine Pact", members = new List<string> { s.playerId, npcs[3].id, npcs[4].id } });
                // The one the round would pick with no founder: who trusts the rest most.
                foreach (var to in new[] { founder, third }) Set(s, trusted, to, 90);
                foreach (var to in new[] { trusted, third }) Set(s, founder, to, -60);
                foreach (var to in new[] { s.playerId, npcs[3].id }) Set(s, npcs[4].id, to, 90);
                foreach (var to in new[] { npcs[3].id, npcs[4].id }) Set(s, s.playerId, to, -60);
                // Nobody in either pact holds the house or sits on the block: every member votes.
                s.hohId = npcs[5].id;
                s.nominees = new List<string> { npcs[6].id, npcs[7].id };
                var snapshot = WebVotingBlocs.FromNative(s);
                var bloc = snapshot.alliances.Single(a => a.id == "alliance-npc-bloc");
                var mine = snapshot.alliances.Single(a => a.id == "alliance-mine");
                string where = rules ? "Under the rules" : "Without them";
                Assert.That(bloc.founderId, Is.EqualTo(rules ? founder : null), where + ": the founder is the first member.");
                Assert.That(mine.founderId, Is.EqualTo(rules ? s.playerId : null), where + ": the player, in a pact they made.");
                var round = WebVotingBlocs.ResolveRound(snapshot).results;
                Assert.That(round.Single(r => r.allianceId == "alliance-npc-bloc").shotCallerId, Is.EqualTo(rules ? founder : trusted),
                    where + ": who calls the bloc's vote.");
                Assert.That(round.Single(r => r.allianceId == "alliance-mine").shotCallerId, Is.EqualTo(rules ? s.playerId : npcs[4].id),
                    where + ": who calls the vote of the player's pact.");

                // A founder on the block cannot call it: the round's own pick does.
                var blocked = s.Clone();
                blocked.nominees = new List<string> { founder, blocked.nominees[1] };
                var called = WebVotingBlocs.ResolveRound(WebVotingBlocs.FromNative(blocked)).results.Single(r => r.allianceId == "alliance-npc-bloc");
                Assert.That(called.shotCallerId, Is.EqualTo(trusted), where + ": with the founder on the block, the round picks.");
            }
        }

        // ------------------------------------------------------------ rename {pact}

        /// <summary>
        /// The founder renames a pact from the names on offer - the house's name for its people, and the
        /// web's - said to a member, free and once a week. A name not on offer is refused: the player's
        /// own words, the pact's name already, another pact of theirs' name. Nothing anybody weighs moves:
        /// no action, no roll, no view, no record; the line goes to everyone in it.
        /// </summary>
        [Test]
        public void UnderTheRulesTheFounderRenamesFromTheNamesOnOfferFreeAndOnceAWeek()
        {
            var s = Rules(Season(89));
            var npcs = Npcs(s);
            var riley = npcs[0];
            var jo = npcs[1];
            var pact = Pact(s, "alliance-name", "The " + riley.name.Split(' ')[0] + " Pact", "player", s.playerId, riley.id, jo.id);
            Pact(s, "alliance-other", "The Outsiders", "player", s.playerId, npcs[2].id);
            var offered = Offered(s, pact);
            string house = "The " + riley.name.Split(' ')[0] + " and " + jo.name.Split(' ')[0] + " Pact";
            Assert.That(offered.First(), Is.EqualTo(house), "First, the house's own name for its people.");
            Assert.That(offered, Does.Not.Contain(pact.name), "Not the name it has,");
            Assert.That(offered, Does.Not.Contain("The Outsiders"), "nor one another pact of the player's has.");
            Assert.That(offered, Does.Contain("Dream Team"), "The web's own names are on offer.");

            foreach (string unoffered in new[] { "Totally Our Name", pact.name, "The Outsiders", "" })
            {
                var refused = Apply(new EpisodeEngine(s), Rename, riley.id, pact.id, unoffered);
                Assert.That(refused.accepted, Is.False, "'" + unoffered + "' is not on offer.");
                Assert.That(refused.reason, Is.EqualTo(NameRefusal));
            }
            var stranger = Apply(new EpisodeEngine(s), Rename, npcs[3].id, pact.id, house);
            Assert.That(stranger.accepted, Is.False, "It is said to a member.");
            Assert.That(stranger.reason, Is.EqualTo("Say it to somebody in " + pact.name + "."));

            int spent = EpisodeEngine.SocialActionsSpent(s);
            var engine = new EpisodeEngine(s);
            var renamed = Apply(engine, Rename, jo.id, pact.id, house);
            Assert.That(renamed.accepted, Is.True, renamed.reason);
            var after = renamed.state;
            Assert.That(after.alliances.Single(a => a.id == pact.id).name, Is.EqualTo(house));
            Assert.That(EpisodeEngine.SocialActionsSpent(after), Is.EqualTo(spent), "Free.");
            Assert.That(after.randomState, Is.EqualTo(s.randomState), "No roll.");
            Assert.That(Json(after.relationships), Is.EqualTo(Json(s.relationships)), "Nobody's view moves, and nothing is on the record.");
            Assert.That(Json(after.alliances.Select(a => a.members)), Is.EqualTo(Json(s.alliances.Select(a => a.members))), "Nobody joins or leaves.");
            var line = after.events.Last();
            Assert.That(line.kind, Is.EqualTo(RenamedKind), "Its own kind: never read as a pact formed or ended.");
            Assert.That(line.text, Is.EqualTo(pact.name + " is " + house + " now."));
            Assert.That(line.audienceIds, Is.EquivalentTo(new[] { s.playerId, riley.id, jo.id }), "Everyone in it hears.");

            var again = Apply(engine, Rename, riley.id, pact.id, "Dream Team");
            Assert.That(again.accepted, Is.False, "Once a week.");
            Assert.That(again.reason, Is.EqualTo(house + " has had its new name this week."));
            var nextWeek = engine.Snapshot;
            nextWeek.week++;
            Assert.That(Apply(new EpisodeEngine(nextWeek), Rename, riley.id, pact.id, "Dream Team").accepted, Is.True, "Next week it can be renamed again.");
        }

        /// <summary>A pact the player was brought into is its founder's to name, not the player's.</summary>
        [Test]
        public void UnderTheRulesOnlyTheFounderRenames()
        {
            var s = Rules(Season(90));
            var npcs = Npcs(s);
            var bloc = NpcAlliances.FormFromStory(s, new List<string> { npcs[1].id, npcs[2].id });
            foreach (string member in bloc.members) { Set(s, member, s.playerId, 30); Set(s, s.playerId, member, 30); }
            Invite(s, npcs[1].id);
            // A command first, so the pact has the ledger row every pact in a season has: a story's.
            var engine = new EpisodeEngine(s);
            Assert.That(Apply(engine, EpisodeCommandKind.SmallTalk, npcs[3].id).accepted, Is.True);
            var now = engine.Snapshot;
            var joined = now.alliances.Single(a => a.id == bloc.id);
            Assert.That(joined.members, Is.EqualTo(new[] { npcs[1].id, npcs[2].id, now.playerId }), "The player is in it, not first.");
            Assert.That(now.ledger.alliances.Single(r => r.id == bloc.id).why, Does.StartWith("story"));
            Assert.That(PactRenameRefusal(now, joined), Is.EqualTo("Only whoever founded " + joined.name + " names it."), "The row says so before it is pressed,");
            var refused = Apply(engine, Rename, npcs[1].id, bloc.id, "Dream Team");
            Assert.That(refused.accepted, Is.False, "and the engine refuses it,");
            Assert.That(refused.reason, Is.EqualTo("Only whoever founded " + joined.name + " names it."));
            Assert.That(refused.state.revision, Is.EqualTo(now.revision), "spending nothing.");
        }

        // ------------------------------------------------------------ leave {pact}

        /// <summary>
        /// Leaving a pact of three takes only the player out: the other two keep it, and keep each other,
        /// and its founder - the player, who made it - is now the first of them. The walk-out's price is
        /// what it always was: the one told thinks fifteen the less of the player, remembers it, and
        /// everybody left behind holds the eighty; the action is spent. Everyone in it hears the line.
        /// </summary>
        [Test]
        public void UnderTheRulesLeavingAPactOfThreeKeepsTheOtherTwoAllied()
        {
            var s = Rules(Season(91));
            EpisodeEngine.EnableStory(s);
            var npcs = Npcs(s);
            var riley = npcs[0];
            var jo = npcs[1];
            var pact = Pact(s, "alliance-three", "The Three Pact", "player", s.playerId, riley.id, jo.id);
            foreach (var a in new[] { s.playerId, riley.id, jo.id })
                foreach (var b in new[] { s.playerId, riley.id, jo.id }.Where(b => b != a)) Set(s, a, b, 30);
            int spent = EpisodeEngine.SocialActionsSpent(s);
            var result = Apply(new EpisodeEngine(s), EpisodeCommandKind.LeaveAlliance, riley.id, pact.id);
            Assert.That(result.accepted, Is.True, result.reason);
            var after = result.state;
            var left = after.alliances.Single(a => a.id == pact.id);
            Assert.That(left.active, Is.True, "The pact goes on");
            Assert.That(left.members, Is.EqualTo(new[] { riley.id, jo.id }), "without the player,");
            Assert.That(after.Allied(riley.id, jo.id), Is.True, "the other two allied,");
            Assert.That(after.Allied(after.playerId, riley.id) || after.Allied(after.playerId, jo.id), Is.False, "and the player with neither.");
            Assert.That(Founder(left), Is.EqualTo(riley.id), "Its first member now is its founder.");
            Assert.That(Grudges.Severity(after, riley.id, after.playerId), Is.EqualTo(80), "Everyone left behind holds the eighty,");
            Assert.That(Grudges.Severity(after, jo.id, after.playerId), Is.EqualTo(80));
            Assert.That(after.Score(riley.id, after.playerId), Is.LessThan(s.Score(riley.id, s.playerId)), "the one told thinks the less of the player,");
            Assert.That(after.memories.Any(m => m.ownerId == riley.id && m.subjectId == after.playerId && m.text == "Left our alliance."), Is.True, "and remembers it.");
            Assert.That(EpisodeEngine.SocialActionsSpent(after), Is.EqualTo(spent + 1), "The action is spent.");
            var line = after.events.Last();
            Assert.That(line.text, Is.EqualTo("You left the alliance with " + riley.name + " and " + jo.name + ": The Three Pact goes on without you."));
            Assert.That(line.audienceIds, Is.EquivalentTo(new[] { after.playerId, riley.id, jo.id }), "Everyone in it hears.");
            Assert.That(after.ledger.alliances.Single(r => r.id == pact.id).endedWeek, Is.Zero, "Its row stays open: it has not ended.");
            Assert.That(EpisodeValidation.TryValidate(after, out string error), Is.True, error);
        }

        /// <summary>A pact of two ends as leaving one always did, with or without the rules, line for line.</summary>
        [Test]
        public void UnderTheRulesLeavingAPactOfTwoEndsItAsItAlwaysDid()
        {
            EpisodeState Left(bool rules)
            {
                var s = Season(92);
                if (rules) EpisodeEngine.EnableCommitments(s);
                EpisodeEngine.EnableStory(s);
                var riley = Npcs(s)[0];
                Pact(s, "alliance-two", "The Two Pact", "player", s.playerId, riley.id);
                Set(s, riley.id, s.playerId, 30); Set(s, s.playerId, riley.id, 30);
                var result = Apply(new EpisodeEngine(s), EpisodeCommandKind.LeaveAlliance, riley.id, rules ? "alliance-two" : null);
                Assert.That(result.accepted, Is.True, result.reason);
                return result.state;
            }
            var on = Left(true);
            var off = Left(false);
            Assert.That(on.alliances.Single(a => a.id == "alliance-two").active, Is.False, "It ends.");
            Assert.That(Json(on.alliances), Is.EqualTo(Json(off.alliances)));
            Assert.That(Json(on.events), Is.EqualTo(Json(off.events)), "The same line.");
            Assert.That(Json(on.relationships), Is.EqualTo(Json(off.relationships)), "The same fifteen, through the same roll.");
            Assert.That(on.randomState, Is.EqualTo(off.randomState));
            Assert.That(Json(on.story.grudges), Is.EqualTo(Json(off.story.grudges)), "The same eighty.");
        }

        /// <summary>
        /// The leave names its pact. Sharing two pacts with Riley - a pair, and a pact of three - the
        /// player leaves the one named and keeps the other; a command naming none leaves the first they
        /// share, as an older command always did; and a pact Riley is not in is no pact to leave them by.
        /// </summary>
        [Test]
        public void UnderTheRulesTheLeaveNamesItsPactWhenTwoAreShared()
        {
            EpisodeState Shared(out ContestantState riley, out ContestantState jo)
            {
                var s = Rules(Season(93));
                var npcs = Npcs(s);
                riley = npcs[0];
                jo = npcs[1];
                Pact(s, "alliance-pair", "The Pair", "player", s.playerId, riley.id);
                Pact(s, "alliance-trio", "The Trio", "player", s.playerId, riley.id, jo.id);
                Pact(s, "alliance-jo", "The Jo Pact", "player", s.playerId, jo.id);
                return s;
            }

            var s1 = Shared(out var r, out var j);
            var fromTrio = Apply(new EpisodeEngine(s1), EpisodeCommandKind.LeaveAlliance, r.id, "alliance-trio").state;
            Assert.That(fromTrio.alliances.Single(a => a.id == "alliance-trio").members, Is.EqualTo(new[] { r.id, j.id }), "The pact named goes on without the player;");
            Assert.That(fromTrio.alliances.Single(a => a.id == "alliance-pair").active, Is.True, "the other is untouched.");
            Assert.That(fromTrio.Allied(fromTrio.playerId, r.id), Is.True, "The two are still allied, in the pair.");

            var s2 = Shared(out r, out j);
            var fromPair = Apply(new EpisodeEngine(s2), EpisodeCommandKind.LeaveAlliance, r.id, "alliance-pair").state;
            Assert.That(fromPair.alliances.Single(a => a.id == "alliance-pair").active, Is.False, "The pair named ends;");
            Assert.That(fromPair.alliances.Single(a => a.id == "alliance-trio").members, Is.EqualTo(new[] { fromPair.playerId, r.id, j.id }), "the trio is untouched.");

            var s3 = Shared(out r, out j);
            var unnamed = Apply(new EpisodeEngine(s3), EpisodeCommandKind.LeaveAlliance, r.id).state;
            Assert.That(unnamed.alliances.Single(a => a.id == "alliance-pair").active, Is.False, "Naming none leaves the first they share.");
            Assert.That(unnamed.alliances.Single(a => a.id == "alliance-trio").members, Does.Contain(unnamed.playerId));

            var s4 = Shared(out r, out j);
            var notTheirs = Apply(new EpisodeEngine(s4), EpisodeCommandKind.LeaveAlliance, r.id, "alliance-jo");
            Assert.That(notTheirs.accepted, Is.False, "Riley is not in The Jo Pact.");
            Assert.That(notTheirs.reason, Is.EqualTo("No shared alliance is active."));
            Assert.That(notTheirs.state.revision, Is.EqualTo(s4.revision), "Nothing is spent.");
        }

        // ------------------------------------------------------------ without the rules

        /// <summary>
        /// The two kinds are appended after LockFinalArgument, so no recorded ordinal moves: a save and a
        /// recorded season store the number.
        /// </summary>
        [Test]
        public void TheNewKindsAreAppendedAfterTheLastOne()
        {
            Assert.That((int)EpisodeCommandKind.LockFinalArgument, Is.EqualTo(58), "The last kind before C5.");
            Assert.That((int)BringIn, Is.EqualTo(59));
            Assert.That((int)Rename, Is.EqualTo(60));
        }

        /// <summary>
        /// A season without the commitment rules - every recorded season, and every season a test builds
        /// directly - has neither new kind: each is refused before anything is spent, drawn or logged.
        /// A leave there ignores any pact it names and leaves the first the two share, whole, as it always
        /// did; and no bloc has a founder. (Tools/SimulationTests' CommitmentRulesSeasonDigests holds 54
        /// such seasons byte for byte.)
        /// </summary>
        [Test]
        public void WithoutTheRulesTheNewKindsAreRefusedAndNothingMoves()
        {
            var s = Season(94);
            var npcs = Npcs(s);
            var riley = npcs[0];
            var jo = npcs[1];
            var maya = npcs[3];
            Pact(s, "alliance-pair", "The Pair", "player", s.playerId, riley.id);
            Pact(s, "alliance-trio", "The Trio", "player", s.playerId, riley.id, jo.id);
            foreach (var npc in npcs) Warm(s, npc.id, s.playerId);
            foreach (var npc in npcs.Where(npc => npc.id != maya.id)) Set(s, npc.id, maya.id, 100);
            string before = Json(s);
            foreach (var (kind, target, second, text) in new[]
            {
                (BringIn, maya.id, "alliance-trio", (string)null),
                (Rename, riley.id, "alliance-trio", "Dream Team"),
            })
            {
                var refused = Apply(new EpisodeEngine(s), kind, target, second, text);
                Assert.That(refused.accepted, Is.False, kind + " without the rules.");
                Assert.That(refused.reason, Is.EqualTo(RulesRefusal));
                Assert.That(Json(refused.state), Is.EqualTo(before), kind + ": nothing moves.");
            }
            var named = Apply(new EpisodeEngine(s), EpisodeCommandKind.LeaveAlliance, riley.id, "alliance-trio");
            var unnamed = Apply(new EpisodeEngine(s), EpisodeCommandKind.LeaveAlliance, riley.id);
            Assert.That(named.accepted && unnamed.accepted, Is.True);
            Assert.That(Json(named.state.alliances), Is.EqualTo(Json(unnamed.state.alliances)), "A leave ignores the pact it names,");
            Assert.That(Json(named.state.events), Is.EqualTo(Json(unnamed.state.events)));
            Assert.That(named.state.randomState, Is.EqualTo(unnamed.state.randomState));
            Assert.That(named.state.alliances.Single(a => a.id == "alliance-pair").active, Is.False, "leaving the first the two share,");
            Assert.That(named.state.alliances.Single(a => a.id == "alliance-trio").members, Does.Contain(s.playerId), "and the trio is untouched.");

            var trioOnly = Season(94);
            Pact(trioOnly, "alliance-trio", "The Trio", "player", trioOnly.playerId, Npcs(trioOnly)[0].id, Npcs(trioOnly)[1].id);
            var whole = Apply(new EpisodeEngine(trioOnly), EpisodeCommandKind.LeaveAlliance, Npcs(trioOnly)[0].id).state;
            Assert.That(whole.alliances.Single(a => a.id == "alliance-trio").active, Is.False, "A pact of three ends for everybody, as it always did.");
            Assert.That(whole.alliances.Single(a => a.id == "alliance-trio").members, Does.Contain(trioOnly.playerId));
            Assert.That(WebVotingBlocs.FromNative(s).alliances.All(a => a.founderId == null), Is.True, "No bloc has a founder.");
        }

        // ------------------------------------------------------------ what the player is shown

        /// <summary>
        /// The walk-out the player can reckon (C4's read): leaving a pact of three leaves everyone in it
        /// holding the eighty, and the pact is no longer theirs - so the read of a proposal to either of
        /// them is 'no chance', from the player's own line, and the proposal is turned down as the read
        /// says. Somebody who was never in it reads as before.
        /// </summary>
        [Test]
        public void UnderTheRulesTheReadKnowsTheGrudgeALeaveFromAPactOfThreeLeft()
        {
            var s = Rules(Season(95));
            EpisodeEngine.EnableStory(s);
            var npcs = Npcs(s);
            var riley = Plain(npcs[0]);
            var jo = Plain(npcs[1]);
            var stranger = Plain(npcs[2]);
            foreach (var npc in new[] { riley, jo, stranger }) Warm(s, npc.id, s.playerId);
            var pact = Pact(s, "alliance-three", "The Three Pact", "player", s.playerId, riley.id, jo.id);
            var engine = new EpisodeEngine(s);
            var left = Apply(engine, EpisodeCommandKind.LeaveAlliance, riley.id, pact.id);
            Assert.That(left.accepted, Is.True, left.reason);
            var after = engine.Snapshot;
            foreach (var member in new[] { riley, jo })
                Assert.That(KnownOdds.Alliance(after, member.id).word, Is.EqualTo(KnownOdds.NoChance), member.name + " holds what the walk-out left, and the read says so.");
            Assert.That(KnownOdds.Alliance(after, stranger.id).word, Is.Not.EqualTo(KnownOdds.NoChance), "Somebody never in it reads as before.");
            var asked = Apply(engine, EpisodeCommandKind.FormAlliance, jo.id);
            Assert.That(asked.accepted, Is.True, asked.reason);
            Assert.That(asked.state.Allied(asked.state.playerId, jo.id), Is.False, "and the proposal is turned down, as the read said.");
        }

        /// <summary>
        /// The alliances page (V3) says what the player did: a pact they made, grown since, was formed
        /// with whoever it was formed with, and says who they brought in and when; a pact of three they
        /// walked out of is no longer theirs, and shows among the pacts they know of by their own line.
        /// </summary>
        [Test]
        public void UnderTheRulesTheAlliancesPageSaysWhoWasBroughtInAndWhichPactWentOnWithoutThePlayer()
        {
            var s = Rules(Season(96));
            var npcs = Npcs(s);
            var riley = Plain(npcs[0]);
            var maya = Plain(npcs[3]);
            var pact = Pact(s, "alliance-grown", "The Riley Pact", "player", s.playerId, riley.id);
            Set(s, riley.id, maya.id, 100);
            Warm(s, maya.id, s.playerId);
            s.randomState = Draw(s, true, PlayerDeals.AcceptanceChance(s, maya.id, DealKind.AllianceInvite, null));
            var engine = new EpisodeEngine(s);
            Assert.That(Apply(engine, BringIn, maya.id, pact.id).accepted, Is.True);
            var card = AllianceRead.Yours(engine.Snapshot).Single(p => p.id == pact.id);
            Assert.That(card.formed, Is.EqualTo("You formed it with " + riley.name.Split(' ')[0] + "."), "Formed with Riley,");
            Assert.That(card.joined.Select(j => (j.week, j.text)), Is.EqualTo(new[] { (s.week, "You brought " + maya.name.Split(' ')[0] + " in.") }),
                "and Maya brought in since.");
            Assert.That(card.members.Select(m => m.id), Is.EqualTo(new[] { riley.id, maya.id }), "Both are in it.");

            // Renamed since, the line still says who joined it: its hearers were the pact's.
            var renamed = Apply(engine, Rename, riley.id, pact.id, "Dream Team");
            Assert.That(renamed.accepted, Is.True, renamed.reason);
            Assert.That(AllianceRead.Yours(engine.Snapshot).Single(p => p.id == pact.id).joined, Has.Count.EqualTo(1), "Renamed, it still knows.");

            var left = Apply(engine, EpisodeCommandKind.LeaveAlliance, riley.id, pact.id);
            Assert.That(left.accepted, Is.True, left.reason);
            var after = engine.Snapshot;
            Assert.That(AllianceRead.Yours(after).Any(p => p.id == pact.id), Is.False, "No longer the player's,");
            var known = AllianceRead.Suspected(after).Single(c => c.memberIds.Contains(riley.id) && c.memberIds.Contains(maya.id));
            Assert.That(known.evidence.Select(e => e.text), Does.Contain("You left the alliance with " + riley.name + " and " + maya.name + ": Dream Team goes on without you."),
                "but known to them by their own line.");
        }

        /// <summary>
        /// What the one asked says (HouseDialogue): brought in, a pact's yes; their own no, a proposal's
        /// no; a member's no, what they make of not being wanted. And the one told a leave from a pact of
        /// three answers as a leave is answered, the pact going on without the player.
        /// </summary>
        [Test]
        public void UnderTheRulesTheOneAskedAnswersForWhatHappened()
        {
            // A pact's yes and a proposal's no are what a proposal answered so is answered with.
            var yes = Asked(true, true, out string id);
            Assert.That(yes.Allied(yes.playerId, id), Is.True);
            string pactYes = HouseDialogue.Response(yes, id, EpisodeCommandKind.FormAlliance);
            Assert.That(HouseDialogue.Response(yes, id, BringIn), Is.EqualTo(pactYes), "In: a pact's yes.");
            var theirNo = Asked(false, true, out id);
            string proposalNo = HouseDialogue.Response(theirNo, id, EpisodeCommandKind.FormAlliance);
            Assert.That(proposalNo, Is.Not.EqualTo(pactYes));
            Assert.That(HouseDialogue.Response(theirNo, id, BringIn), Is.EqualTo(proposalNo), "Their own no, said as a proposal's is, without a reason.");
            var membersNo = Asked(true, false, out id);
            Assert.That(MembersNoReplies, Does.Contain(HouseDialogue.Response(membersNo, id, BringIn)), "A member's no: what they make of not being wanted.");

            var s = Rules(Season(98));
            var npcs = Npcs(s);
            Pact(s, "alliance-three", "The Three Pact", "player", s.playerId, npcs[0].id, npcs[1].id);
            var engine = new EpisodeEngine(s);
            Assert.That(Apply(engine, EpisodeCommandKind.LeaveAlliance, npcs[0].id, "alliance-three").accepted, Is.True);
            Assert.That(LeftReplies, Does.Contain(HouseDialogue.Response(engine.Snapshot, npcs[0].id, EpisodeCommandKind.LeaveAlliance)),
                "The one told answers a leave, though no ended pact is left between them.");
        }

        /// <summary>What the one asked says when a member would not have them (HouseDialogue.Response), in each voice.</summary>
        private static readonly object[] MembersNoReplies =
        {
            "If they won't have me, I'm not going to beg to be let in. Thank you for asking, though.",
            "Sounds like your people have opinions about me. Noted.",
            "Oh. They don't want me in it. That's... all right. Thank you for trying.",
            "Rejected by committee. A new low, and I've sat on the block.",
            "A pact needs everyone's yes, and I didn't get one. That's how it should work.",
            "It sounds like the others aren't ready for me. Thanks for asking.",
        };

        /// <summary>What the one told a leave says in its first week (HouseDialogue.Response), in each voice.</summary>
        private static readonly object[] LeftReplies =
        {
            "You've left our alliance. I'll treat that as a change in our agreement, not an unspoken favor.",
            "You left our alliance. All right. I'm playing my own game from here.",
            "You left our alliance. That hurts, but I'd rather know where we stand.",
            "So our alliance is over. Awkward, but at least nobody has to guess.",
            "The alliance is over. I'll stop making plans that depend on it.",
            "Our alliance has ended. We'll need to rebuild trust before making another plan.",
        };

        /// <summary>A season with the one asked's answer and the members' say set, after the asking.</summary>
        private static EpisodeState Asked(bool willing, bool welcome, out string inviteeId)
        {
            var s = Rules(Season(97));
            var npcs = Npcs(s);
            var riley = Plain(npcs[0]);
            var maya = Plain(npcs[3]);
            inviteeId = maya.id;
            var pact = Pact(s, "alliance-asked", "The Asked Pact", "player", s.playerId, riley.id);
            Set(s, riley.id, maya.id, welcome ? 100 : 0);
            Warm(s, maya.id, s.playerId);
            s.randomState = Draw(s, willing, PlayerDeals.AcceptanceChance(s, maya.id, DealKind.AllianceInvite, null));
            var result = Apply(new EpisodeEngine(s), BringIn, maya.id, pact.id);
            Assert.That(result.accepted, Is.True, result.reason);
            return result.state;
        }

        // ------------------------------------------------------------ the slice's own API
        //
        // Every name C5 added is read through these, and only these, so the file compiles against the
        // build before it (1597bc0) with their bodies stubbed to that build's behaviour - which is how
        // each test above was seen to fail there.

        private static EpisodeCommandKind BringIn => EpisodeCommandKind.BringIntoAlliance;

        private static EpisodeCommandKind Rename => EpisodeCommandKind.RenameAlliance;

        private static bool Welcome(EpisodeState s, string memberId, string inviteeId) => NpcAlliances.WouldWelcome(s, memberId, inviteeId);

        private static List<string> Offered(EpisodeState s, AllianceState pact) => PactNames.For(s, pact);

        private static string Refusal(EpisodeState s, string inviteeId, AllianceState pact) => EpisodeEngine.BringInRefusal(s, inviteeId, pact);

        private static string PactRenameRefusal(EpisodeState s, AllianceState pact) => EpisodeEngine.RenameRefusal(s, pact);

        private static string Founder(AllianceState pact) => EpisodeEngine.Founder(pact);

        private static string JoinedLine(string name, string pact) => EpisodeEngine.JoinedLine(name, pact);

        private static int Largest => EpisodeEngine.LargestPact;

        private static string NotYours => EpisodeEngine.NotYourPactRefusal;

        private static string NameRefusal => EpisodeEngine.NameNotOfferedRefusal;

        private static string RulesRefusal => EpisodeEngine.CommitmentKindRefusal;

        private static string RenamedKind => EpisodeEngine.AllianceRenamedKind;

        private static string RefusedKind => EpisodeEngine.AllianceRefusedKind;

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

        /// <summary>The two warm on each other, so the one asked is likely to say yes.</summary>
        private static void Warm(EpisodeState s, string npcId, string playerId)
        {
            Set(s, npcId, playerId, 100);
            Set(s, playerId, npcId, 100);
        }

        /// <summary>A pact, with the ledger row every pact a season makes has: it began this week, and <paramref name="why"/> says where it came from.</summary>
        private static AllianceState Pact(EpisodeState s, string id, string name, string why, params string[] members)
        {
            var pact = new AllianceState { id = id, name = name, members = members.ToList(), active = true };
            s.alliances.Add(pact);
            s.ledger.alliances.Add(new AllianceRow { id = id, startedWeek = s.week, why = why });
            return pact;
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

        private static string Json(object value) => JsonConvert.SerializeObject(value);

        private static List<RelationshipEventState> Entries(EpisodeState s, string from, string to, string type) =>
            s.relationships.Where(r => r.fromId == from && r.toId == to).SelectMany(r => r.events).Where(e => e.type == type).ToList();

        private static EpisodeCommand Command(EpisodeState s, EpisodeCommandKind kind, string target = null, string second = null, string text = null) =>
            new EpisodeCommand
            {
                id = "grow-" + kind + "-" + s.revision, actorId = s.playerId, kind = kind,
                targetId = target, secondTargetId = second, text = text, expectedRevision = s.revision, expectedPhase = s.phase,
            };

        private static CommandResult Apply(EpisodeEngine engine, EpisodeCommandKind kind, string target = null, string second = null, string text = null) =>
            engine.Apply(Command(engine.Snapshot, kind, target, second, text));

        /// <summary>An invitation agreed, as the engine writes its pact (the private AllyThroughInvitation).</summary>
        private static void Invite(EpisodeState s, string npcId) =>
            typeof(EpisodeEngine).GetMethod("AllyThroughInvitation", BindingFlags.NonPublic | BindingFlags.Static)
                .Invoke(null, new object[] { s, npcId });
    }
}
