using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Gamesim.Episode;
using Gamesim.Simulation;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;

namespace Gamesim.Tests.PlayMode
{
    /// <summary>
    /// The war room on screen (WAVE-D-NPC-PACTS-PLAN D3-S6): a pact of three of the player's meets once the
    /// block is set - the meeting's row says "warmth · plan", and in free time no row offers it - and its
    /// open plan is a card in any conversation with somebody who was there: its heading, each say with the
    /// player's read of the member's vote, and the answers found by their captions. Pushing for the other
    /// nominee is one command and one line, and the card is gone; the alliances page says the plan. An open
    /// plan survives a reload. Every house size the card is drawn in (6, 8 and 12), at both text sizes, with
    /// the longest names, every label stands in a box at least 1.3 times its words and draws them whole;
    /// photographed in a batch run as 'conversation-war-room', '-large' at the larger text and '-4x3' on a
    /// 4:3 frame. The engine's half is PactPlanTests, the presentation's pure half PactPlanPresentationTests.
    /// </summary>
    public sealed partial class EpisodePlayModeTests
    {
        private const string WarRoomPactId = "alliance-war-room-test", WarRoomPactName = "The War Room";

        /// <summary>The longest names the card has to hold, given to the pact's members and the block.</summary>
        private static readonly string[] WarRoomLongNames =
            { "Anastasia Featherstonehaugh", "Bartholomew Okonkwo-Lindqvist", "Guinevere Montgomery-Ashford", "Maximilian Van der Westhuizen" };

        /// <summary>
        /// The story's staging rules, the commitment rules and the war rooms (the campaign's fixture has the
        /// levers), and the player in a pact with two who vote this week - the one the tests talk to among
        /// them - everybody in it warm, both of them set on evicting the first nominee. With
        /// <paramref name="open"/> its war room has been held: the plan is open on the ledger, as the engine
        /// writes it. The two on the block and the two in the pact carry the longest names.
        /// </summary>
        private static System.Action<EpisodeState> WarRoomHouse(bool open) => state =>
        {
            EpisodeEngine.EnableStory(state);
            EpisodeEngine.EnableCommitments(state);
            EpisodeEngine.EnablePactPlans(state);
            var voters = EpisodeEngine.Voters(state).Where(v => !v.isPlayer).Select(v => v.id).ToList();
            // The one Listener picks first - in the house, voting, holding nothing - then the one after.
            string first = voters.FirstOrDefault(id => id != state.vetoHolderId) ?? voters[0];
            string second = voters.First(id => id != first);
            state.alliances.RemoveAll(pact => pact.members.Contains(state.playerId));
            var members = new List<string> { state.playerId, first, second };
            state.alliances.Add(new AllianceState { id = WarRoomPactId, name = WarRoomPactName, active = true, members = members });
            state.ledger.alliances.Add(new AllianceRow { id = WarRoomPactId, startedWeek = state.week, why = "player" });
            foreach (string from in members)
                foreach (string to in members.Where(id => id != from)) WarRoomScore(state, from, to, 40);
            foreach (string voter in new[] { first, second })
            {
                WarRoomScore(state, voter, state.nominees[0], -60);
                WarRoomScore(state, voter, state.nominees[1], 40);
            }
            var named = new[] { first, second, state.nominees[0], state.nominees[1] };
            for (int i = 0; i < named.Length; i++) state.Find(named[i]).name = WarRoomLongNames[i];
            if (!open) return;
            var pact = state.alliances.Single(a => a.id == WarRoomPactId);
            var at = EpisodeEngine.AtTheMeeting(state, pact);
            state.ledger.plans.Add(new PactPlanRow
            {
                week = state.week, allianceId = WarRoomPactId, throughId = first, stance = PactPlanStance.Open,
                present = new List<string> { state.playerId }.Concat(at).ToList(), says = PactPlans.Says(state, pact),
            });
        };

        private static void WarRoomScore(EpisodeState state, string from, string to, double score)
        {
            var edge = state.relationships.FirstOrDefault(r => r.fromId == from && r.toId == to);
            if (edge == null) state.relationships.Add(edge = new RelationshipState { fromId = from, toId = to });
            edge.score = score;
        }

        /// <summary>The war room's pact as the house holds it now.</summary>
        private AllianceState WarRoomPact() => director.Snapshot.alliances.Single(a => a.id == WarRoomPactId);

        [UnityTest, Timeout(1800000)]
        public IEnumerator WarRoom_ThePlanCardFitsInEveryHouseAtBothTextSizes()
        {
            foreach (int size in new[] { 6, 8, 12 })
            foreach (bool larger in new[] { false, true })
            {
                string where = "The war room's card in a house of " + size + (larger ? " at the larger text" : " at the standard text");
                yield return InstallTalkingHouse(size, true, WarRoomHouse(true));
                yield return SettleCast();
                yield return ApplyTextSize(larger);
                var listener = Listener(director.Snapshot);
                Assert.That(WarRoomPact().members, Has.Member(listener.id), where + ": precondition, the one talked to was at the meeting.");
                yield return TalkInRoom(listener.id, "Bedroom");
                Assert.That(director.IsConversationOpen, Is.True, where + ": the conversation opens.");
                var state = director.Snapshot;
                var pact = WarRoomPact();
                Assert.That(EpisodeDirector.PlanCardPact(state, listener.id)?.id, Is.EqualTo(WarRoomPactId), where + ": the plan is open.");
                // The answers below go with the first nominee and push for the second: both who vote must have said the first.
                Assert.That(PactPlans.MembersPlan(PactPlans.StandingSays(state, pact, PactPlans.OpenPlan(state, WarRoomPactId))), Is.EqualTo(state.nominees[0]),
                    where + ": precondition, the members' plan is the first nominee.");

                // The heading, each say, and the answers, found by their captions.
                Assert.That(ShownText(), Does.Contain(PactPlans.Heading(WarRoomPactName)), where + ": the card's heading.");
                foreach (var line in PactPlans.CardFacts(state, pact, PactPlans.OpenPlan(state, WarRoomPactId)))
                    Assert.That(ShownText(), Does.Contain(PactPlans.CardText(line)), where + ": '" + PactPlans.CardText(line) + "'.");
                var answers = EpisodeDirector.PlanAnswers(state, pact);
                Assert.That(answers.Select(a => a.caption), Is.EqualTo(new[]
                {
                    EpisodeDirector.PlanAgreeCaption(state.Find(state.nominees[0]).name),
                    EpisodeDirector.PlanCounterCaption(state.Find(state.nominees[1]).name),
                    EpisodeDirector.PlanLieLowCaption,
                }), where + ": go with it, push for the other, or lie low.");
                foreach (var (caption, _) in answers)
                {
                    var row = ButtonWithCaption(caption);
                    Assert.That(TagOn(row), Is.EqualTo(EpisodeDirector.PlanAnswerTag), where + ": '" + caption + "' is free.");
                }
                Assert.That(director.GetComponentsInChildren<UnityEngine.UI.Button>().Where(b => b.IsActive())
                    .SelectMany(b => b.GetComponentsInChildren<TMP_Text>(true)).Any(t => t.text.StartsWith("Call the vote in " + WarRoomPactName, System.StringComparison.Ordinal)),
                    Is.False, where + ": a pact of three takes no call of its own.");
                AssertConversationFits(larger, where);
                AssertTagsHaveRoom(where);
                foreach (var label in director.GetComponentsInChildren<TMP_Text>()
                             .Where(label => label.gameObject.activeInHierarchy && (label.text == PactPlans.Heading(WarRoomPactName)
                                 || answers.Any(a => a.caption == label.text))))
                    AssertLineHasRoom(label, where);
                if (size == 8) yield return CaptureConversation("conversation-war-room" + (larger ? "-large" : ""), where, null);
                director.ClosePanels();
                yield return null;
            }
            yield return ApplyTextSize(false);
        }

        [UnityTest, Timeout(900000)]
        public IEnumerator WarRoom_MeetsOnceTheBlockIsSetAndItsRowSaysSo()
        {
            // Free time: no row offers a meeting of a pact of three.
            yield return InstallTalkingHouse(8, false, state =>
            {
                EpisodeEngine.EnableLevers(state);
                var npcs = state.Active.Where(c => !c.isPlayer).Select(c => c.id).ToList();
                EpisodeEngine.EnableStory(state);
                EpisodeEngine.EnableCommitments(state);
                EpisodeEngine.EnablePactPlans(state);
                state.alliances.RemoveAll(pact => pact.members.Contains(state.playerId));
                var members = new List<string> { state.playerId, npcs[3], npcs[4] };
                state.alliances.Add(new AllianceState { id = WarRoomPactId, name = WarRoomPactName, active = true, members = members });
                state.ledger.alliances.Add(new AllianceRow { id = WarRoomPactId, startedWeek = state.week, why = "player" });
                foreach (string from in members)
                    foreach (string to in members.Where(id => id != from)) WarRoomScore(state, from, to, 40);
            });
            yield return SettleCast();
            var mate = WarRoomPact().members.First(id => id != director.Snapshot.playerId);
            yield return TalkInRoom(mate, "Bedroom");
            Assert.That(director.IsConversationOpen, Is.True);
            Assert.That(ButtonWithCaptionOrNull(EpisodeDirector.AllianceMeetingCaption), Is.Null, "No meeting of a pact of three before the block is set.");
            director.ClosePanels();
            yield return null;

            // The campaign: the row is offered, and its pill says a plan comes of it.
            yield return InstallTalkingHouse(8, true, WarRoomHouse(false));
            yield return SettleCast();
            var listener = Listener(director.Snapshot);
            yield return TalkInRoom(listener.id, "Bedroom");
            var row = ButtonWithCaption(EpisodeDirector.AllianceMeetingCaption);
            Assert.That(TagOn(row), Is.EqualTo(EpisodeDirector.WarmthTag + " · " + EpisodeDirector.PlanTag));
            Assert.That(TagOn(row), Is.EqualTo(EpisodeDirector.AllianceMeetingTag(director.Snapshot, WarRoomPact())));
            director.ClosePanels();
            yield return null;
        }

        [UnityTest, Timeout(900000)]
        public IEnumerator WarRoom_ACounterIsOneLineAndTheCardIsGoneAndThePageSaysThePlan()
        {
            yield return InstallTalkingHouse(8, true, WarRoomHouse(false));
            yield return SettleCast();
            yield return ApplyTextSize(false);
            var listener = Listener(director.Snapshot);
            yield return TalkInRoom(listener.id, "Bedroom");
            int revision = director.Snapshot.revision;
            ButtonWithCaption(EpisodeDirector.AllianceMeetingCaption).onClick.Invoke();
            yield return null; yield return null;
            var met = director.Snapshot;
            Assert.That(met.revision, Is.EqualTo(revision + 1), "The war room is one command.");
            Assert.That(PactPlans.OpenPlan(met, WarRoomPactId), Is.Not.Null, "Its plan is open.");
            Assert.That(PactPlans.MembersPlan(PactPlans.OpenPlan(met, WarRoomPactId).says), Is.EqualTo(met.nominees[0]),
                "Precondition: the members' plan is the first nominee, so pushing for the second is the counter.");
            Assert.That(met.events.Last(e => e.kind == "conversation").text, Does.StartWith(WarRoomPactName + " met where nobody listens: you, "));

            // The card, in the conversation the player comes back to.
            yield return TalkInRoom(listener.id, "Bedroom");
            string other = met.nominees[1];
            string counter = EpisodeDirector.PlanCounterCaption(met.Find(other).name);
            // Read once the conversation is open: opening a panel can commit the house's own tick.
            var opened = director.Snapshot;
            ButtonWithCaption(counter).onClick.Invoke();
            yield return null; yield return null;
            var after = director.Snapshot;
            Assert.That(after.revision, Is.EqualTo(opened.revision + 1), "The answer is one command,");
            Assert.That(after.nextSequence, Is.EqualTo(opened.nextSequence + 1), "one line,");
            var line = after.events.Last();
            Assert.That(line.kind, Is.EqualTo(WaveDEventKinds.PactPlan));
            Assert.That(line.text, Does.StartWith("You pushed for " + after.Find(other).name + "."));
            Assert.That(after.randomState, Is.EqualTo(opened.randomState), "no draw from the season's stream,");
            Assert.That(ButtonWithCaptionOrNull(EpisodeDirector.PlanLieLowCaption), Is.Null, "and the card is gone.");
            Assert.That(ButtonWithCaptionOrNull(counter), Is.Null);
            director.ClosePanels();
            yield return null;

            // The alliances page says the plan, after the pact's calls.
            var plan = AllianceRead.Read(after).yours.Single(p => p.id == WarRoomPactId).plans.Single();
            director.ShowNotebookSection(EpisodeDirector.NotebookSection.Alliances);
            yield return null; yield return null;
            Canvas.ForceUpdateCanvases();
            Assert.That(CopyUnder(LastActive(EpisodeHud.AllianceCardName(WarRoomPactName, WarRoomPactId))), Does.Contain("Week " + after.week + " · " + plan.text),
                "The plan's line on the pact's card.");
            Assert.That(plan.text, Does.StartWith("Plan: "));
            Assert.That(plan.text, Does.Not.Match("[0-9]"), "No number.");
            director.ClosePanels();
            yield return null;
        }

        [UnityTest, Timeout(900000)]
        public IEnumerator WarRoom_AnOpenPlanSurvivesAReload()
        {
            yield return InstallTalkingHouse(8, true, WarRoomHouse(true));
            yield return SettleCast();
            var before = director.Snapshot;
            Assert.That(PactPlans.OpenPlan(before, WarRoomPactId), Is.Not.Null);
            director.SaveNow();
            director.LoadNow();
            yield return SettleCast();
            HoldTheHouseForTheFixture();
            var loaded = director.Snapshot;
            var row = PactPlans.OpenPlan(loaded, WarRoomPactId);
            Assert.That(row, Is.Not.Null, "The plan is still open after the reload,");
            Assert.That(row.says.Select(say => (say.memberId, say.targetId)),
                Is.EqualTo(PactPlans.OpenPlan(before, WarRoomPactId).says.Select(say => (say.memberId, say.targetId))), "with its says.");
            var listener = Listener(loaded);
            yield return TalkInRoom(listener.id, "Bedroom");
            Assert.That(ButtonWithCaptionOrNull(EpisodeDirector.PlanLieLowCaption), Is.Not.Null, "and the card redraws from it.");
            director.ClosePanels();
            yield return null;
        }
    }
}
