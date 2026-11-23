using System;
using System.Collections.Generic;
using System.Linq;
using Gamesim.Simulation;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// Dialogue rules 4's nominee grant (DIALOGUE-PLAN D5a, §4): a nominated player's one conversation a week on the house,
    /// played on genuine seasons enrolled by the literal 4 (rules 4 is defined and not offered). The grant is written at the
    /// nominations, pays for one conversation in place of the window's action (never the veto question), is used up by the
    /// conversation's first node and stays used if it lapses or is left; at most two free conversations a week; a greedy
    /// player finds no loop. Shared cases: the dotnet subset runs them.
    /// </summary>
    public sealed class DialogueRules4GrantTests
    {
        private const string NoActionLeft = "You have no conversations left this week.";

        private static EpisodeState Accepted(EpisodeEngine engine, EpisodeCommand command) => DialogueRules2Season.Accepted(engine, command);

        private static EpisodeState Next(EpisodeEngine engine)
        {
            var next = EpisodeEngineTests.NextCommand(engine.Snapshot);
            if (next.kind == EpisodeCommandKind.Compete) next.performance = 0;
            return Accepted(engine, next);
        }

        private static DialogueRoleGrantState Grant(EpisodeState s) =>
            s.dialogue.grants.SingleOrDefault(g => g.id == DialogueRules.GrantId(s.week, DialogueRules.NomineeRole));

        private static EpisodeCommand SavePlea(EpisodeState s, string holder, string approach)
        {
            var c = DialogueRules2Season.Command(s, EpisodeCommandKind.Lobby);
            c.targetId = holder; c.secondTargetId = s.playerId; c.text = LobbyAsk.Encode(LobbyAsk.Save, approach);
            return c;
        }

        private static EpisodeState WithoutTheGrant(EpisodeState s)
        {
            var copy = s.Clone();
            copy.dialogue.grants.Clear();
            Assert.That(EpisodeValidation.TryValidate(copy, out string error), Is.True, "The same season with no grant: " + error);
            return copy;
        }

        // ---------------------------------------------------------------- the seasons

        private static DialogueRules3Season.Stop meeting;

        /// <summary>
        /// The first rules-4 veto meeting in seeds 1 to 60 (weeks 1 to 5) with the player nominated, the holder somebody else
        /// off the block, the decision open, the asks answered and the appeal to the holder startable. One person: the holder.
        /// </summary>
        private static DialogueRules3Season.Stop Meeting()
        {
            if (meeting != null) return meeting;
            for (uint seed = 1; seed <= 60; seed++)
            {
                var engine = new EpisodeEngine(DialogueRules4Season.Start(seed));
                for (int step = 0; step < 600; step++)
                {
                    var s = engine.Snapshot;
                    if (s.week > 5 || s.phase == EpisodePhase.Finished || s.Find(s.playerId).status != ContestantStatus.Active) break;
                    if (s.phase == EpisodePhase.VetoMeeting && !s.vetoResolved && s.nominees.Contains(s.playerId)
                        && s.vetoHolderId != null && s.vetoHolderId != s.playerId && !s.nominees.Contains(s.vetoHolderId))
                    {
                        DialogueRules2Season.CloseAsks(engine);
                        s = engine.Snapshot;
                        if (DialogueRules.CanStart(s, s.vetoHolderId, out _))
                        {
                            TestContext.Out.WriteLine("Rules-4 veto meeting: seed " + seed + ", week " + s.week + ", revision " + s.revision + ".");
                            return meeting = new DialogueRules3Season.Stop { Seed = seed, State = s.Clone(), People = new[] { s.vetoHolderId } };
                        }
                    }
                    Next(engine);
                }
            }
            Assert.Fail("No rules-4 season of 60 met a veto meeting with the player nominated and the holder free to talk.");
            return null;
        }

        private sealed class Spent
        {
            internal DialogueRules4Season.Stop Stop;
            internal EpisodeState Left, Meeting;
        }

        private static Spent spent;

        /// <summary>
        /// The window's one seat after the nominations spent on the veto question: from the first veto-question stop where
        /// asking a veto player and leaving leaves no action, played on to a veto meeting with the holder somebody else off
        /// the block and the appeal to them on the house. The AP-0 meeting the plan's case is extended to.
        /// </summary>
        private static Spent SeatSpent()
        {
            if (spent != null) return spent;
            foreach (var stop in DialogueRules4Season.Stops())
            {
                var engine = new EpisodeEngine(stop.State.Clone());
                var asked = Accepted(engine, DialogueRules4Season.Ask(stop.State, stop.People[0]));
                var left = Accepted(engine, DialogueRules2Season.Choice(asked, DialogueVetoQuestion.Leave));
                if (DialogueRules2Season.Remaining(left) != 0) continue;
                Accepted(engine, DialogueRules4Season.Compete(left));
                for (int step = 0; step < 12 && engine.Snapshot.phase != EpisodePhase.VetoMeeting; step++) Next(engine);
                var s = engine.Snapshot;
                if (s.phase != EpisodePhase.VetoMeeting || s.vetoResolved || !s.nominees.Contains(s.playerId)
                    || s.vetoHolderId == null || s.vetoHolderId == s.playerId || s.nominees.Contains(s.vetoHolderId)) continue;
                DialogueRules2Season.CloseAsks(engine);
                s = engine.Snapshot;
                if (DialogueRules2Season.Remaining(s) != 0 || !DialogueRules.OnTheHouseWith(s, s.vetoHolderId)) continue;
                TestContext.Out.WriteLine("Rules-4 seat spent: seed " + stop.Seed + ", week " + s.week + ".");
                return spent = new Spent { Stop = stop, Left = left, Meeting = s.Clone() };
            }
            Assert.Fail("No rules-4 stop spent its seat on the veto question and met an appeal to the holder on the house.");
            return null;
        }

        // ---------------------------------------------------------------- the cases

        /// <summary>The nominations write the player's grant, exactly; a season below rules 4 holds none and refuses one.</summary>
        [Test]
        public void TheNominationsWriteTheNomineesGrantAndNothingBelowRulesFour()
        {
            var s = Meeting().State;
            var grant = Grant(s);
            Assert.That(grant, Is.Not.Null, "The nominated player's grant of this week.");
            Assert.That((grant.ownerId, grant.week, grant.role, grant.sourceKind, grant.sourcePhase, grant.status),
                Is.EqualTo((s.playerId, s.week, DialogueRules.NomineeRole, DialogueRules.FromNominations, EpisodePhase.Nomination, DialogueRules.GrantOpen)));
            Assert.That(grant.consumedEpisodeId, Is.Null);
            Assert.That(grant.consumedRevision, Is.Zero);
            Assert.That(grant.expiryReason, Is.Null);
            Assert.That(grant.sourceNominees, Does.Contain(s.playerId));
            Assert.That(DialogueRules.FreeConversationsUsed(s, s.week), Is.Zero);
            Assert.That(DialogueRules.OnTheHouseWith(s, Meeting().People[0]), Is.True);
            Assert.That(s.dialogue.grants.All(g => g.ownerId == s.playerId && g.role == DialogueRules.NomineeRole), Is.True);
            Assert.That(s.dialogue.grants.Select(g => g.week).Distinct().Count(), Is.EqualTo(s.dialogue.grants.Count), "One a week.");

            var rules3 = s.Clone();
            rules3.dialogueRulesVersion = DialogueRules.Rules3;
            rules3.dialogue.vetoOccasions.Clear();
            Assert.That(EpisodeValidation.TryValidate(rules3, out string error), Is.False, "Rules 3 holds no grant.");
            Assert.That(error, Does.Contain("Only action-funded campaign conversations are supported by this version."));
            rules3.dialogue.grants.Clear();
            Assert.That(EpisodeValidation.TryValidate(rules3, out error), Is.True, error);
            Assert.That(DialogueRules.GrantPaysNow(rules3), Is.False);
            Assert.That(DialogueRules.OnTheHouseWith(rules3, Meeting().People[0]), Is.False);
            Assert.That(DialogueRules3Season.FindVetoMeeting().State.dialogue.grants, Is.Empty, "A rules-3 season writes none.");
        }

        /// <summary>The action count of the week with and without the grant: the grant pays the entry, and nothing else moves.</summary>
        [Test]
        public void TheGrantPaysForOneConversationInPlaceOfTheAction()
        {
            var stop = Meeting();
            var before = stop.State;
            string holder = stop.People[0];
            int seat = before.windowActions[Windows.AfterNominations];
            var opened = Accepted(new EpisodeEngine(before.Clone()), DialogueRules3Season.Entry(before, holder, DialogueRules3Season.Holder));
            var episode = DialogueRules3Season.Open(opened);
            Assert.That((episode.entry.kind, episode.entry.grantId, episode.entry.windowActionBefore, episode.entry.windowActionAfter),
                Is.EqualTo((DialogueRules.GrantEntry, Grant(before).id, seat, seat)));
            Assert.That(opened.windowActions, Is.EqualTo(before.windowActions), "No action: the grant paid.");
            Assert.That(EpisodeEngine.SocialActionsSpent(opened), Is.EqualTo(EpisodeEngine.SocialActionsSpent(before)));
            Assert.That((opened.socialActions, opened.outOfPhaseSocialActions), Is.EqualTo((before.socialActions, before.outOfPhaseSocialActions)));
            var used = Grant(opened);
            Assert.That((used.status, used.consumedEpisodeId, used.consumedRevision), Is.EqualTo((DialogueRules.GrantUsed, episode.id, opened.revision)));
            Assert.That(DialogueRules.FreeConversationsUsed(opened, opened.week), Is.EqualTo(1));
            Assert.That(DialogueRules.GrantPaysNow(opened), Is.False, "One conversation, then the grant is spent.");
            Assert.That(opened.randomState, Is.EqualTo(before.randomState), "The grant draws nothing.");

            var paid = Accepted(new EpisodeEngine(WithoutTheGrant(before)), DialogueRules3Season.Entry(before, holder, DialogueRules3Season.Holder));
            var paidEpisode = DialogueRules3Season.Open(paid);
            Assert.That((paidEpisode.entry.kind, paidEpisode.entry.windowActionBefore, paidEpisode.entry.windowActionAfter),
                Is.EqualTo((DialogueRules.ActionEntry, seat, seat + 1)));
            Assert.That(paid.windowActions[Windows.AfterNominations], Is.EqualTo(seat + 1), "Without the grant the same conversation costs the action.");
            Assert.That(paidEpisode.responses[0].responseKey, Is.EqualTo(episode.responses[0].responseKey), "The same conversation either way.");
        }

        /// <summary>A conversation the grant paid for that lapses at the decision, or is left, keeps the grant used.</summary>
        [Test]
        public void AGrantUsedByAConversationThatLapsesOrIsLeftStaysUsed()
        {
            var stop = Meeting();
            string holder = stop.People[0];
            var engine = new EpisodeEngine(stop.State.Clone());
            var opened = Accepted(engine, DialogueRules3Season.Entry(stop.State, holder, DialogueRules3Season.Holder));
            string id = DialogueRules3Season.Open(opened).id;

            var left = Accepted(new EpisodeEngine(opened.Clone()), DialogueRules2Season.Choice(opened, DialogueHolderAppeal.Leave));
            Assert.That(DialogueRules.Active(left), Is.Null);
            Assert.That((Grant(left).status, Grant(left).consumedEpisodeId), Is.EqualTo((DialogueRules.GrantUsed, id)), "Left, it stays used.");
            Assert.That(DialogueRules.GrantPaysNow(left), Is.False);

            // The decision lapses the open appeal inside the same command that writes the week's veto decision.
            var lapsed = Accepted(engine, EpisodeEngineTests.NextCommand(opened));
            var episode = lapsed.dialogue.episodes.Single(e => e.id == id);
            Assert.That((DialogueRules.Cycle(lapsed, episode).endingId, episode.time.reason), Is.EqualTo(("lapsed", "occasion-closed")));
            Assert.That(lapsed.vetoResolved, Is.True);
            Assert.That(lapsed.dialogue.vetoOccasions.Single(o => o.week == lapsed.week).decision, Is.Not.Null, "The decision is recorded.");
            Assert.That((Grant(lapsed).status, Grant(lapsed).consumedEpisodeId), Is.EqualTo((DialogueRules.GrantUsed, id)), "Lapsed, it stays used.");
            Assert.That(DialogueRules.FreeConversationsUsed(lapsed, lapsed.week), Is.EqualTo(1));

            for (int step = 0; step < 12 && engine.Snapshot.phase != EpisodePhase.Campaign; step++) Next(engine);
            DialogueRules2Season.CloseAsks(engine);
            var campaign = engine.Snapshot;
            Assert.That(campaign.phase, Is.EqualTo(EpisodePhase.Campaign));
            Assert.That(DialogueRules.GrantPaysNow(campaign), Is.False, "No second conversation on the house this week.");
            string voter = EpisodeEngine.Voters(campaign).Where(c => !c.isPlayer).Select(c => c.id).FirstOrDefault(v => DialogueRules.CanStart(campaign, v, out _));
            if (campaign.nominees.Contains(campaign.playerId) && voter != null)
            {
                var talked = Accepted(engine, DialogueRules3Season.Entry(campaign, voter, DialogueRules3Season.Trees));
                Assert.That(DialogueRules3Season.Open(talked).entry.kind, Is.EqualTo(DialogueRules.ActionEntry));
                Assert.That(talked.windowActions[Windows.AfterVeto], Is.EqualTo(campaign.windowActions[Windows.AfterVeto] + 1));
            }
        }

        /// <summary>The veto question always costs the window's action (decision 10): asked with the grant open, the grant stays open.</summary>
        [Test]
        public void TheVetoQuestionIsNeverOnTheHouse()
        {
            var s = SeatSpent();
            var before = s.Stop.State;
            Assert.That(Grant(before)?.status, Is.EqualTo(DialogueRules.GrantOpen), "The grant is open as the question is asked.");
            Assert.That(DialogueRules.GrantPaysNow(before), Is.False, "The veto question's phase: no grant pays.");
            var question = DialogueRules4Season.Question(s.Left, s.Stop.People[0]);
            Assert.That(question.entry.kind, Is.EqualTo(DialogueRules.ActionEntry));
            Assert.That(s.Left.windowActions[Windows.AfterNominations], Is.EqualTo(before.windowActions[Windows.AfterNominations] + 1));
            Assert.That(Grant(s.Left).status, Is.EqualTo(DialogueRules.GrantOpen), "The question used no grant.");
            Assert.That(DialogueRules2Season.Remaining(s.Left), Is.Zero);
            Assert.That(DialogueRules.GrantPaysNow(s.Left), Is.False);
            foreach (string other in s.Stop.People.Skip(1))
            {
                Assert.That(DialogueRules.CanStart(s.Left, other, out _), Is.False, other + ": with the seat spent, the grant asks no second veto player.");
                Assert.That(DialogueRules.OnTheHouseWith(s.Left, other), Is.False, other);
            }
        }

        /// <summary>
        /// The plan's AP-0 case extended to the grant (OWNER-DECISIONS §4; DIALOGUE-PLAN §4): at AP 0 the paid save plea is
        /// refused in every approach; without the grant no route reaches the holder; with it the appeal opens for no action
        /// and its final plea is the one plea delivered, once; after it the paid plea is still refused.
        /// </summary>
        [Test]
        public void AtApZeroTheGrantIsTheOnlyRouteToThePleaAndOnlyOnce()
        {
            var s = SeatSpent();
            var meeting = s.Meeting;
            string holder = meeting.vetoHolderId;
            Assert.That(DialogueRules2Season.Remaining(meeting), Is.Zero);
            foreach (string approach in LobbyApproach.All)
                Assert.That(DialogueRules2Season.Refused(new EpisodeEngine(meeting.Clone()), SavePlea(meeting, holder, approach)), Is.EqualTo(NoActionLeft), approach);

            var none = WithoutTheGrant(meeting);
            Assert.That(DialogueRules.CanStart(none, holder, out string why), Is.False);
            Assert.That(why, Is.EqualTo(NoActionLeft), "Without the grant the appeal is refused at AP 0.");
            Assert.That(DialogueRules.TryBuildEntryCommand(none, holder, out _), Is.False);

            var engine = new EpisodeEngine(meeting.Clone());
            var opened = Accepted(engine, DialogueRules3Season.Entry(meeting, holder, DialogueRules3Season.Holder));
            Assert.That(DialogueRules2Season.Remaining(opened), Is.Zero, "Opened on the house.");
            Assert.That(opened.lobbies.Count, Is.EqualTo(meeting.lobbies.Count), "Opening delivers no plea.");
            var reacted = Accepted(engine, DialogueRules2Season.Choice(opened, DialogueHolderAppeal.Appeal));
            var pleaded = Accepted(engine, DialogueRules2Season.Choice(reacted, DialogueHolderAppeal.AskToUse));
            var rows = pleaded.lobbies.Where(l => l.week == pleaded.week && l.phase == EpisodePhase.VetoMeeting && l.deciderId == holder).ToList();
            Assert.That(rows.Count, Is.EqualTo(1), "The one plea, at the conversation's end.");
            Assert.That((rows[0].ask, rows[0].subjectId, rows[0].approach), Is.EqualTo((LobbyAsk.Save, pleaded.playerId, LobbyApproach.Emotional)));
            Assert.That(DialogueRules2Season.Remaining(pleaded), Is.Zero);
            foreach (string approach in LobbyApproach.All)
                DialogueRules2Season.Refused(new EpisodeEngine(pleaded.Clone()), SavePlea(pleaded, holder, approach));
            Assert.That(DialogueRules.CanStart(pleaded, holder, out _), Is.False);
            Assert.That(DialogueRules.GrantPaysNow(pleaded), Is.False);
        }

        /// <summary>At most two free conversations a week, whatever pays: a third is never on the house, and the validator refuses one.</summary>
        [Test]
        public void AWeekHasAtMostTwoFreeConversations()
        {
            var s = SeatSpent();
            var opened = Accepted(new EpisodeEngine(s.Meeting.Clone()), DialogueRules3Season.Entry(s.Meeting, s.Meeting.vetoHolderId, DialogueRules3Season.Holder));
            var left = Accepted(new EpisodeEngine(opened.Clone()), DialogueRules2Season.Choice(opened, DialogueHolderAppeal.Leave));
            Assert.That(DialogueRules.FreeConversationsAWeek, Is.EqualTo(2));

            // The week as the bound reads it: the grant re-opened, with one free conversation had, pays; with two, it does not.
            var one = left.Clone();
            var grant = Grant(one);
            grant.status = DialogueRules.GrantOpen; grant.consumedEpisodeId = null; grant.consumedRevision = 0;
            Assert.That(DialogueRules.FreeConversationsUsed(one, one.week), Is.EqualTo(1));
            Assert.That(DialogueRules.GrantPaysNow(one), Is.True);
            var two = one.Clone();
            two.dialogue.episodes.Single(e => e.occasionKind == DialogueVetoQuestion.Kind && e.week == two.week).entry.kind = DialogueRules.GrantEntry;
            Assert.That(DialogueRules.FreeConversationsUsed(two, two.week), Is.EqualTo(2));
            Assert.That(DialogueRules.GrantPaysNow(two), Is.False, "Two free conversations this week: no third.");

            // A third free conversation in the week is refused by the store before anything else about it is read.
            var three = left.Clone();
            var free = three.dialogue.episodes.Single(e => e.entry.kind == DialogueRules.GrantEntry);
            three.dialogue.episodes.Add(free.Clone());
            three.dialogue.episodes.Add(free.Clone());
            Assert.That(DialogueRules.TryValidate(three, out string error), Is.False);
            Assert.That(error, Is.EqualTo("A week has at most two free conversations."));
        }

        /// <summary>
        /// A greedy scripted player (§4: "A greedy scripted player hunts for loops in D2 and D5"): across whole seasons it takes
        /// every conversation on the house it can, leaves it at once, reloads, and tries every houseguest again. It never finds
        /// a second free conversation in a week: one grant a week it is nominated in, each used by exactly one conversation.
        /// </summary>
        [Test]
        public void AGreedyPlayerFindsNoLoopInTheGrants()
        {
            int free = 0, used = 0;
            for (uint seed = 1; seed <= 6; seed++)
            {
                var states = Greedy(seed);
                var last = states.Last();
                free += last.dialogue.episodes.Count(e => e.entry.kind == DialogueRules.GrantEntry);
                used += last.dialogue.grants.Count(g => g.status == DialogueRules.GrantUsed);
                foreach (var s in states)
                {
                    Assert.That(s.dialogue.grants.GroupBy(g => g.week).All(g => g.Count() == 1), Is.True, "One grant a week.");
                    foreach (var week in s.dialogue.episodes.Select(e => e.week).Distinct())
                        Assert.That(DialogueRules.FreeConversationsUsed(s, week),
                            Is.LessThanOrEqualTo(s.dialogue.grants.Count(g => g.week == week && g.status == DialogueRules.GrantUsed)),
                            "Week " + week + ": every free conversation spent a grant of its own week.");
                }
            }
            TestContext.Out.WriteLine("Greedy seasons 1 to 6: " + free + " conversations on the house, " + used + " grants used.");
            Assert.That(free, Is.GreaterThan(0), "The hunt took conversations on the house.");
            Assert.That(free, Is.EqualTo(used), "Each free conversation used exactly one grant.");
        }

        private static readonly Dictionary<uint, List<EpisodeState>> greedy = new Dictionary<uint, List<EpisodeState>>();

        /// <summary>One whole rules-4 season of the greedy player; the states it accepted, in order. Cached per seed.</summary>
        private static List<EpisodeState> Greedy(uint seed)
        {
            if (greedy.TryGetValue(seed, out var known)) return known;
            var states = new List<EpisodeState>();
            var engine = new EpisodeEngine(DialogueRules4Season.Start(seed));
            for (int step = 0; step < 1500 && engine.Snapshot.phase != EpisodePhase.Finished; step++)
            {
                var s = engine.Snapshot;
                if (DialogueRules.Active(s) == null && DialogueRules.GrantPaysNow(s) && s.Find(s.playerId).status == ContestantStatus.Active
                    && (s.phase == EpisodePhase.Campaign || s.phase == EpisodePhase.VetoMeeting))
                {
                    DialogueRules2Season.CloseAsks(engine);
                    s = engine.Snapshot;
                    string npc = s.Active.Where(c => !c.isPlayer).Select(c => c.id).FirstOrDefault(id => DialogueRules.OnTheHouseWith(s, id));
                    if (npc != null)
                    {
                        Assert.That(DialogueRules.TryBuildEntryCommand(s, npc, out var plan), Is.True);
                        plan.id = "greedy-" + s.revision + "-" + npc;
                        var opened = Accepted(engine, plan);
                        Assert.That(DialogueRules.Active(opened).entry.kind, Is.EqualTo(DialogueRules.GrantEntry));
                        Assert.That(opened.windowActions, Is.EqualTo(s.windowActions), "On the house.");
                        var left = Accepted(engine, DialogueRules2Season.Choice(opened, DialogueCampaignTrees.Leave));
                        states.Add(left);
                        // The loop hunt: after the free one, every houseguest again, then a reload, and again.
                        var reloaded = DialogueRules2Season.RoundTrip(left);
                        Assert.That(EpisodeValidation.TryValidate(reloaded, out string error), Is.True, error);
                        foreach (var state in new[] { left, reloaded })
                        {
                            Assert.That(DialogueRules.GrantPaysNow(state), Is.False, "Week " + state.week + ": no second free conversation.");
                            Assert.That(state.Active.Where(c => !c.isPlayer).Any(c => DialogueRules.OnTheHouseWith(state, c.id)), Is.False);
                        }
                        continue;
                    }
                }
                states.Add(Next(engine));
            }
            Assert.That(engine.Snapshot.phase, Is.EqualTo(EpisodePhase.Finished), "Seed " + seed + ": the season ends.");
            greedy[seed] = states;
            return states;
        }

        /// <summary>A season saved mid-week with the grant unused reloads with it, and the grant pays as if never saved.</summary>
        [Test]
        public void ASeasonSavedWithAnUnusedGrantReloadsWithIt()
        {
            var stop = Meeting();
            var loaded = DialogueRules2Season.RoundTrip(stop.State);
            Assert.That(EpisodeValidation.TryValidate(loaded, out string error), Is.True, error);
            Assert.That(DialogueRules2Season.Json(loaded), Is.EqualTo(DialogueRules2Season.Json(stop.State)));
            Assert.That(Grant(loaded).status, Is.EqualTo(DialogueRules.GrantOpen));
            Assert.That(DialogueRules.OnTheHouseWith(loaded, stop.People[0]), Is.True);
            var afterLoad = Accepted(new EpisodeEngine(loaded), DialogueRules3Season.Entry(loaded, stop.People[0], DialogueRules3Season.Holder));
            var live = Accepted(new EpisodeEngine(stop.State.Clone()), DialogueRules3Season.Entry(stop.State, stop.People[0], DialogueRules3Season.Holder));
            Assert.That(DialogueRules2Season.Json(afterLoad), Is.EqualTo(DialogueRules2Season.Json(live)));
        }

        [TestCase("second-grant")] [TestCase("role")] [TestCase("owner")] [TestCase("used-by-nothing")]
        [TestCase("open-but-spent")] [TestCase("entry-moves-the-action")]
        public void TheValidatorRefusesAGrantThatStrays(string defect)
        {
            var stop = Meeting();
            var opened = Accepted(new EpisodeEngine(stop.State.Clone()), DialogueRules3Season.Entry(stop.State, stop.People[0], DialogueRules3Season.Holder));
            var bad = (defect == "open-but-spent" || defect == "entry-moves-the-action" ? opened : stop.State).Clone();
            var grant = Grant(bad);
            switch (defect)
            {
                case "second-grant": bad.dialogue.grants.Add(grant.Clone()); break;
                case "role": grant.role = "head-of-household"; break;
                case "owner": grant.ownerId = stop.People[0]; break;
                case "used-by-nothing": grant.status = DialogueRules.GrantUsed; grant.consumedEpisodeId = "story-1"; grant.consumedRevision = 1; break;
                case "open-but-spent": grant.status = DialogueRules.GrantOpen; grant.consumedEpisodeId = null; grant.consumedRevision = 0; break;
                default: DialogueRules.Active(bad).entry.windowActionAfter += 1; break;
            }
            Assert.That(EpisodeValidation.TryValidate(bad, out string error), Is.False, defect);
            TestContext.Out.WriteLine(defect + ": " + error);
        }
    }
}
