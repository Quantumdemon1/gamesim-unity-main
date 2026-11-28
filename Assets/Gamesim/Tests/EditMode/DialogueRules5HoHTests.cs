using System;
using System.Collections.Generic;
using System.Linq;
using Gamesim.Simulation;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// Dialogue rules 5's HoH consultation (DIALOGUE-PLAN D5b-1, §4 and §4.1), played on genuine seasons enrolled by the
    /// literal 5 (rules 5 is defined and switched off: the chip still writes 4, so nothing else starts one). The player as Head
    /// of Household calls a houseguest in before the original nominations: on the house while the HoH grant pays, otherwise
    /// for the window's action; a promise in it is the existing promise of safety and never a second safety row; it lapses as
    /// the nominations commit; it draws only keyed coins; rules 4 refuses its rows. Shared cases: the dotnet subset runs them.
    /// </summary>
    public sealed class DialogueRules5HoHTests
    {
        private const string Template = DialogueHoHConsult.TemplateId;

        private static EpisodeState Accepted(EpisodeEngine engine, EpisodeCommand command) => DialogueRules2Season.Accepted(engine, command);

        /// <summary>The shipped house of a seed, before its first command, enrolled on dialogue rules <paramref name="rules"/> by its literal.</summary>
        private static EpisodeState Start(uint seed, int rules)
        {
            var fresh = SeasonBuilder.Create(new SeasonBuilder.Choice { HouseSize = 8 }, seed);
            ShippedRules.ApplyFresh(fresh);
            Assert.That(fresh.dialogueRulesVersion, Is.Zero, "A shipped season starts on dialogue rules 0.");
            fresh.dialogueRulesVersion = rules;
            return fresh;
        }

        /// <summary>The walk's next command: the plain public walk, every competition played flat out so the player can win the HoH.</summary>
        private static EpisodeCommand Next(EpisodeState s)
        {
            var next = EpisodeEngineTests.NextCommand(s);
            if (next.kind == EpisodeCommandKind.Compete) next.performance = 1;
            return next;
        }

        private sealed class Stop
        {
            internal uint Seed;
            internal EpisodeState State;
            internal string[] People;
        }

        private static Stop stop;

        /// <summary>
        /// The first nominations in seeds 1 to 60 (weeks 1 to 5) with the player Head of Household and nobody named, the asks
        /// answered, two seats left in the window and at least two houseguests to call in. Returned as a detached copy.
        /// </summary>
        private static Stop Nominations()
        {
            if (stop != null) return stop;
            for (uint seed = 1; seed <= 60; seed++)
            {
                var engine = new EpisodeEngine(Start(seed, DialogueRules.Rules5));
                for (int step = 0; step < 600; step++)
                {
                    var s = engine.Snapshot;
                    if (s.week > 5 || s.phase == EpisodePhase.Finished || s.Find(s.playerId).status != ContestantStatus.Active) break;
                    if (s.phase == EpisodePhase.Nomination && s.nominees.Count == 0 && s.hohId == s.playerId)
                    {
                        DialogueRules2Season.CloseAsks(engine);
                        s = engine.Snapshot;
                        var people = s.Active.Where(c => !c.isPlayer).Select(c => c.id).Where(id => DialogueRules.CanCallIn(s, id)).ToArray();
                        if (people.Length >= 2 && DialogueRules2Season.Remaining(s) >= 2)
                        {
                            TestContext.Out.WriteLine("Rules-5 nominations: seed " + seed + ", week " + s.week + ", revision " + s.revision + ".");
                            return stop = new Stop { Seed = seed, State = s.Clone(), People = people };
                        }
                    }
                    Accepted(engine, Next(s));
                }
            }
            Assert.Fail("No rules-5 season of 60 met nominations with the player Head of Household and two houseguests to call in.");
            return null;
        }

        /// <summary>The entry the game page builds: the public plan, never a hand-made payload.</summary>
        private static EpisodeCommand CallIn(EpisodeState s, string npcId)
        {
            Assert.That(DialogueRules.CanCallIn(s, npcId), Is.True, "The game page offers the consultation.");
            Assert.That(DialogueRules.TryBuildEntryCommand(s, npcId, out var plan), Is.True, "The HUD's own entry plan.");
            Assert.That(plan.text, Is.EqualTo(Template));
            plan.id = "dialogue-r5-call-" + s.revision + "-" + npcId;
            return plan;
        }

        private static EpisodeCommand Choice(EpisodeState s, string option) => DialogueRules2Season.Choice(s, option);

        private static DialogueEpisodeState Consultation(EpisodeState s, string npcId) =>
            s.dialogue.episodes.Single(e => e.occasionKind == DialogueHoHConsult.Kind && e.npcId == npcId && e.week == s.week);

        private static string Suffix(DialogueResponseState response) => response.responseKey.Substring(Template.Length + 1);

        private static DialogueRoleGrantState HoHGrant(EpisodeState s) =>
            s.dialogue.grants.SingleOrDefault(g => g.id == DialogueRules.GrantId(s.week, DialogueRules.HoHRole));

        private static int SafetyRows(EpisodeState s, string npcId) => CommitmentReferences.Promises(s).Count(p => p.status == PromiseStatus.Active
            && p.fromId == s.playerId && p.toId == npcId && p.kind == PromiseKind.Safety);

        private static EpisodeCommand Nominate(EpisodeState s)
        {
            var c = DialogueRules2Season.Command(s, EpisodeCommandKind.Nominate);
            var pool = EpisodeEngine.NominationCandidates(s).ToArray();
            c.targetId = pool[0].id; c.secondTargetId = pool[1].id;
            return c;
        }

        // ---------------------------------------------------------------- the cases

        /// <summary>
        /// The window (§4.1): the consultation opens in the nominations, before the original nominations, with the Head of
        /// Household's grant written as they opened; each houseguest once a week; it lapses as the nominations commit, drawing
        /// nothing, and is not offered again - nor granted again - once anybody is named.
        /// </summary>
        [Test]
        public void TheConsultationOpensBeforeTheNominationsAndLapsesWhenTheyCommit()
        {
            var at = Nominations();
            var s = at.State;
            var grant = HoHGrant(s);
            Assert.That(grant, Is.Not.Null, "The Head of Household's grant was written as the nominations opened.");
            Assert.That(grant.status == DialogueRules.GrantOpen && grant.sourceKind == DialogueRules.FromCrowning
                && grant.sourcePhase == EpisodePhase.Nomination && grant.sourceNominees.Count == 0, Is.True);
            string npc = at.People[0];
            var engine = new EpisodeEngine(s.Clone());
            var opened = Accepted(engine, CallIn(s, npc));
            var talk = Consultation(opened, npc);
            Assert.That(talk.occasionId, Is.EqualTo(DialogueRules.HoHConsultOccasion(s.week, npc)));
            Assert.That(talk.window, Is.EqualTo(Windows.AfterHoH));
            Assert.That(DialogueRules.Active(opened), Is.Not.Null, "The consultation is open on its reply node.");
            var left = Accepted(new EpisodeEngine(opened.Clone()), Choice(opened, DialogueHoHConsult.Leave));
            Assert.That(DialogueRules.CanStart(left, npc, out string again), Is.False, "Once a week for each houseguest.");
            Assert.That(again, Is.EqualTo(DialogueRules.HoHConsultUsedRefusal));

            // The consultation left open lapses as the nominations commit: its own exit, the occasion closed.
            var named = Accepted(engine, Nominate(opened));
            var lapsed = Consultation(named, npc);
            var cycle = DialogueRules2Season.Cycle(named, lapsed);
            Assert.That(cycle.status, Is.EqualTo(StorylineStatus.Completed));
            Assert.That(cycle.endingId, Is.EqualTo("lapsed"));
            Assert.That(lapsed.time.reason, Is.EqualTo(DialogueHoHConsult.OccasionClosed));
            Assert.That(Suffix(lapsed.responses.Last()), Is.EqualTo(DialogueHoHConsult.OccasionClosed));
            Assert.That(named.nominees.Count, Is.EqualTo(2), "The nominations committed.");
            foreach (var other in named.Active.Where(c => !c.isPlayer))
            {
                Assert.That(DialogueRules.CanCallIn(named, other.id), Is.False, other.id + " cannot be called in once anybody is named.");
                Assert.That(DialogueRules.CanStart(named, other.id, out string closed), Is.False);
                Assert.That(closed, Is.EqualTo(DialogueRules.HoHConsultRefusal));
            }
            // The week goes on to the veto: no second HoH grant, whatever is named later.
            for (int step = 0; step < 40 && engine.Snapshot.week == named.week && engine.Snapshot.phase != EpisodePhase.Campaign; step++)
                Accepted(engine, Next(engine.Snapshot));
            Assert.That(engine.Snapshot.dialogue.grants.Count(g => g.week == named.week && g.role == DialogueRules.HoHRole), Is.EqualTo(1),
                "One HoH grant a week, never granted again for a replacement.");
        }

        /// <summary>
        /// The grant and the two-a-week bound (§4): the first consultation is on the house - the grant used by its first node,
        /// no seat spent - and the next costs the window's action; the free conversation counts against the week's two, and a
        /// save that spends the one grant twice is refused.
        /// </summary>
        [Test]
        public void TheHoHGrantPaysForTheFirstAndTheWindowsActionForTheNext()
        {
            var at = Nominations();
            var s = at.State;
            string first = at.People[0], second = at.People[1];
            Assert.That(DialogueRules.CallInOnTheHouse(s, first), Is.True, "The grant pays while it is open.");
            var engine = new EpisodeEngine(s.Clone());
            var opened = Accepted(engine, CallIn(s, first));
            var talk = Consultation(opened, first);
            Assert.That(talk.entry.kind, Is.EqualTo(DialogueRules.GrantEntry));
            Assert.That(talk.entry.grantId, Is.EqualTo(DialogueRules.GrantId(s.week, DialogueRules.HoHRole)));
            Assert.That(HoHGrant(opened).status, Is.EqualTo(DialogueRules.GrantUsed), "Used by the conversation's first node.");
            Assert.That(HoHGrant(opened).consumedEpisodeId, Is.EqualTo(talk.id));
            Assert.That(opened.windowActions[Windows.AfterHoH], Is.EqualTo(s.windowActions[Windows.AfterHoH]), "No seat was spent.");
            Assert.That(EpisodeEngine.SocialActionsSpent(opened), Is.EqualTo(EpisodeEngine.SocialActionsSpent(s)));
            Assert.That(DialogueRules.FreeConversationsUsed(opened, s.week), Is.EqualTo(1), "The week's free conversations count it.");
            var left = Accepted(engine, Choice(opened, DialogueHoHConsult.Leave));
            Assert.That(HoHGrant(left).status, Is.EqualTo(DialogueRules.GrantUsed), "A grant stays used when its conversation is left.");
            Assert.That(DialogueRules.GrantPaysNow(left), Is.False, "One grant a role a week.");
            Assert.That(DialogueRules.CallInOnTheHouse(left, second), Is.False);

            var paid = Accepted(engine, CallIn(left, second));
            var next = Consultation(paid, second);
            Assert.That(next.entry.kind, Is.EqualTo(DialogueRules.ActionEntry), "The next consultation costs the window's action.");
            Assert.That(paid.windowActions[Windows.AfterHoH], Is.EqualTo(left.windowActions[Windows.AfterHoH] + 1));
            Assert.That(DialogueRules.FreeConversationsUsed(paid, s.week), Is.EqualTo(1));

            // A save whose second consultation claims the spent grant as well is refused.
            var forged = paid.Clone();
            var claim = forged.dialogue.episodes.Single(e => e.id == next.id).entry;
            claim.kind = DialogueRules.GrantEntry; claim.grantId = talk.entry.grantId; claim.windowActionAfter = claim.windowActionBefore;
            forged.windowActions[Windows.AfterHoH]--;
            Assert.That(EpisodeValidation.TryValidate(forged, out string error), Is.False, "One grant pays for one conversation.");
            TestContext.Out.WriteLine("Refused: " + error);
        }

        /// <summary>
        /// A promise in the consultation (§4's table) is the existing promise of safety, never a second safety row to that
        /// houseguest that week: to one already promised it is reaffirmed, and to one not yet promised it is the one row.
        /// </summary>
        [Test]
        public void APromiseInTheConsultationIsNeverASecondSafetyRow()
        {
            var at = Nominations();
            var s = at.State;
            string promised = at.People[0], fresh = at.People[1];
            var engine = new EpisodeEngine(s.Clone());
            var word = DialogueRules2Season.Command(s, EpisodeCommandKind.PromiseSafety);
            word.targetId = promised;
            var given = Accepted(engine, word);
            Assert.That(SafetyRows(given, promised), Is.EqualTo(1), "The player's word, given the ordinary way first.");

            var opened = Accepted(engine, CallIn(given, promised));
            var replied = Accepted(engine, Choice(opened, DialogueHoHConsult.Reassure));
            Assert.That(DialogueHoHConsult.ReactionOf(DialogueRules2Season.Cycle(replied, Consultation(replied, promised)).beatId), Is.Not.Null);
            var again = Accepted(engine, Choice(replied, DialogueHoHConsult.PromiseSafety));
            Assert.That(Suffix(Consultation(again, promised).responses.Last()), Is.EqualTo(DialogueHoHConsult.Reaffirmed));
            Assert.That(SafetyRows(again, promised), Is.EqualTo(1), "Reaffirmed: never a second safety row to them.");
            Assert.That(DialogueRules2Season.Cycle(again, Consultation(again, promised)).endingId, Is.EqualTo(DialogueHoHConsult.PromisedEnding));

            var other = Accepted(engine, CallIn(again, fresh));
            var otherReplied = Accepted(engine, Choice(other, DialogueHoHConsult.Reassure));
            var made = Accepted(engine, Choice(otherReplied, DialogueHoHConsult.PromiseSafety));
            Assert.That(Suffix(Consultation(made, fresh).responses.Last()), Is.EqualTo(DialogueHoHConsult.Promised));
            Assert.That(SafetyRows(made, fresh), Is.EqualTo(1), "A promise made in the consultation is the one safety row.");
            Assert.That(SafetyRows(made, promised), Is.EqualTo(1));
        }

        /// <summary>
        /// Validation refuses rules 5's rows below rules 5: the consultation's content, and the HoH grant on its own. The same
        /// season at rules 5 is admitted.
        /// </summary>
        [Test]
        public void RulesFiveRowsAreRefusedAtRulesFour()
        {
            var at = Nominations();
            var withoutGrant = at.State.Clone();
            withoutGrant.dialogue.grants.Clear();
            Assert.That(EpisodeValidation.TryValidate(withoutGrant, out string error), Is.True, "Rules 5 with no grant: " + error);
            var engine = new EpisodeEngine(withoutGrant);
            var opened = Accepted(engine, CallIn(withoutGrant, at.People[0]));
            var left = Accepted(engine, Choice(opened, DialogueHoHConsult.Leave));
            Assert.That(Consultation(left, at.People[0]).entry.kind, Is.EqualTo(DialogueRules.ActionEntry));

            var content = left.Clone();
            content.dialogueRulesVersion = DialogueRules.Rules4;
            Assert.That(EpisodeValidation.TryValidate(content, out error), Is.False, "A consultation is rules 5's content.");
            Assert.That(error, Is.EqualTo("Unknown dialogue content is not supported by this version."));

            var grant = at.State.Clone();
            grant.dialogueRulesVersion = DialogueRules.Rules4;
            Assert.That(EpisodeValidation.TryValidate(grant, out error), Is.False, "The HoH grant is rules 5's row.");
            Assert.That(error, Is.EqualTo("A role grant must keep the nomination that wrote it."));
        }

        /// <summary>
        /// The consultation draws only keyed coins (§5.2): opened on the house, replied to (its reaction's change is the
        /// player's act toward the houseguest, through the keyed reducer) and closed without a promise, the season's stream
        /// stands where it stood and no pair of houseguests moves.
        /// </summary>
        [Test]
        public void TheConsultationDrawsNothingFromTheSeasonsStream()
        {
            var at = Nominations();
            var s = at.State;
            string npc = at.People[0];
            var engine = new EpisodeEngine(s.Clone());
            var opened = Accepted(engine, CallIn(s, npc));
            var replied = Accepted(engine, Choice(opened, DialogueHoHConsult.Press));
            var closed = Accepted(engine, Choice(replied, DialogueHoHConsult.NoPromise));
            Assert.That(DialogueRules2Season.Cycle(closed, Consultation(closed, npc)).status, Is.EqualTo(StorylineStatus.Completed));
            Assert.That(closed.randomState, Is.EqualTo(s.randomState), "Nothing drawn from the season's stream.");
            Assert.That(DialogueRules4Season.NpcScores(closed), Is.EqualTo(DialogueRules4Season.NpcScores(s)), "No pair of houseguests moved.");
            Assert.That(Consultation(closed, npc).time.appliedDelta, Is.Zero, "The consultation carries no conversation time.");
        }

        /// <summary>
        /// Switched off is the same season (rules 0 to 4 unchanged): the same seed and commands at rules 4 reach the same
        /// nominations with the same stream and sequence, no grant and no consultation; the HoH grant draws nothing.
        /// </summary>
        [Test]
        public void RulesFourWalksTheSameSeasonWithNoHoHGrant()
        {
            var at = Nominations();
            // The same walk, at rules 4, to the same week's nominations.
            var engine = new EpisodeEngine(Start(at.Seed, DialogueRules.Rules4));
            EpisodeState four = null;
            for (int step = 0; step < 600 && four == null; step++)
            {
                var s = engine.Snapshot;
                if (s.week > at.State.week || s.phase == EpisodePhase.Finished) break;
                if (s.phase == EpisodePhase.Nomination && s.nominees.Count == 0 && s.hohId == s.playerId)
                {
                    DialogueRules2Season.CloseAsks(engine);
                    s = engine.Snapshot;
                    if (s.week == at.State.week) { four = s; break; }
                }
                Accepted(engine, Next(s));
            }
            Assert.That(four, Is.Not.Null, "The same nominations at rules 4.");
            Assert.That(four.revision, Is.EqualTo(at.State.revision), "The same commands reached them.");
            Assert.That(four.randomState, Is.EqualTo(at.State.randomState), "The same stream.");
            Assert.That(four.nextSequence, Is.EqualTo(at.State.nextSequence), "The same sequence: the grant logs nothing.");
            Assert.That(four.dialogue.grants, Is.Empty, "No HoH grant below rules 5.");
            Assert.That(at.People.Any(id => DialogueRules.CanCallIn(four, id)), Is.False, "No consultation below rules 5.");
        }
    }
}
