using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Gamesim.Episode;
using Gamesim.House;
using Gamesim.Persistence;
using Gamesim.Presentation;
using Gamesim.Simulation;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Gamesim.Tests.PlayMode
{
    /// <summary>
    /// Houseguests bring their asks to the player (OFFERS-COME-TO-YOU-PLAN, slices 1 and 2): the asker walks over, says
    /// their line and the dialogue opens by itself on the exact offer; talking to somebody with an ask opens on it; one
    /// who cannot come puts it to the player as a card where they stand; nothing a visit, a hearing, a card, Think it
    /// over or a miss does commits anything. Every test opts in after the fixture's reload
    /// turns asks off, pins scaled time at 30 fps (unscaled time, which the asks run on, is not pinned) and bounds its
    /// waits in real seconds or on the ask clock, never in frames.
    /// </summary>
    public sealed partial class EpisodePlayModeTests
    {
        private const string ComesMaya = ContentCatalog.MayaId, ComesJamie = "jamie-roberts";

        /// <summary>
        /// The invitation flow's saved house - offers from Maya and Jamie, every action spent - with asks on. The switch
        /// takes effect first (its reset and the first session key both clear the set-aside), then the named askers'
        /// records are held aside, and the walk-in watch is held. Returns once a visit has been reserved, from somebody
        /// not held aside. No overview.
        /// </summary>
        private IEnumerator InstallComesToYouHouse(params string[] holdAsideAskers)
        {
            yield return OptIntoComesToYou(holdAsideAskers);
            float deadline = Time.realtimeSinceStartup + 35f;
            while (director.CurrentNpcInvitationRecordId == null && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(director.CurrentNpcInvitationRecordId, Is.Not.Null, "An asker sets out for the player. " + director.NpcAutonomyDiagnostic);
            Assert.That(holdAsideAskers, Has.No.Member(director.CurrentAskerId));
            foreach (var id in HeldAside(holdAsideAskers)) Assert.That(director.IsAskSetAside(id), Is.True, id + " is still held aside.");
        }

        /// <summary>
        /// The house saved and reloaded, every body assembled, asks on and taken effect, the named askers held aside; no
        /// visit waited for.
        /// </summary>
        private IEnumerator OptIntoComesToYou(params string[] holdAsideAskers) => OptIntoComesToYouHouse(false, false, holdAsideAskers);

        /// <summary>
        /// <see cref="OptIntoComesToYou"/> on the house of its choice: with <paramref name="threeAsks"/>, Taylor asks too
        /// (OFFERS-COME-TO-YOU-PLAN test 15); with <paramref name="story"/>, the story system is on, so a walk-in can be
        /// offered as a Pull.
        /// </summary>
        private IEnumerator OptIntoComesToYouHouse(bool threeAsks, bool story, string[] holdAsideAskers)
        {
            SaveNpcInvitationFlowState(threeAsks);
            if (story) SaveWithTheStoryOn();
            yield return ReloadEpisode();
            NpcWrite("proximityWatch", -1e6f);
            // Asks are still off meanwhile, so at most the invitation sets out, and the switch below lets it go.
            yield return WaitForBodiesPairingNobody();
            director.AsksEnabled = true;
            float deadline = Time.realtimeSinceStartup + 2f;
            while (!NpcRead<bool>("asksWereOn") && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(NpcRead<bool>("asksWereOn"), Is.True, "The switch takes effect.");
            Assert.That(director.AsksAreOn, Is.True);
            Assert.That(director.CurrentNpcInvitationRecordId, Is.Null, "No visit before the settle and still clocks have run.");
            var aside = NpcRead<HashSet<string>>("askSetAside");
            foreach (var asker in holdAsideAskers)
            {
                var ids = ComesToYou.Read(director.Snapshot).Where(ask => ask.askerId == asker).Select(ask => ask.recordId).ToList();
                Assert.That(ids, Is.Not.Empty, asker + " has an ask to hold aside.");
                foreach (var id in ids) aside.Add(id);
            }
        }

        /// <summary>
        /// The invitation flow's house with asks off, as InstallNpcInvitationFlowHouse leaves it, the walk-in watch held
        /// and every body assembled before the invitation waited for sets out: whatever set out while they assembled
        /// starts afresh, as on a load.
        /// </summary>
        private IEnumerator InstallLegacyInvitationHouse()
        {
            SaveNpcInvitationFlowState();
            yield return ReloadEpisode();
            NpcWrite("proximityWatch", -1e6f);
            yield return WaitForBodiesPairingNobody();
            Assert.That(director.AsksAreOn, Is.False);
            NpcInvoke("ResetNpcInvitationsForLoad");
            Assert.That(director.ShowOverview(), Is.True);
            yield return WaitForVisibleNpcInvitation();
        }

        /// <summary>
        /// Waits until every body has assembled - one finishing later re-renders the HUD and re-selects a control - with
        /// the house's route planning held meanwhile, so the wait pairs nobody: the askers are as free when it ends as just
        /// after the reload, and planning runs as it always does from there.
        /// </summary>
        private IEnumerator WaitForBodiesPairingNobody()
        {
            NpcWrite("npcApproachDiagnosticsSuppressed", true);
            try { yield return WaitForBodies(); }
            finally { NpcWrite("npcApproachDiagnosticsSuppressed", false); }
            Assert.That(NpcRead<IList>("npcApproaches"), Is.Empty, "Nobody set out to pair while the bodies assembled.");
        }

        private IEnumerable<string> HeldAside(string[] askers) =>
            NpcDeals.Pending(director.Snapshot).Where(row => askers.Contains(row.proposerId)).Select(row => row.id);

        /// <summary>Waits for the conversation to open on the asker, bounded in real seconds; prints why a walk missed.</summary>
        private IEnumerator WaitForAskDialogue(string askerId, float seconds = 35f)
        {
            float deadline = Time.realtimeSinceStartup + seconds;
            while (director.TalkingToId == null && Time.realtimeSinceStartup < deadline) yield return null;
            if (director.TalkingToId == null)
            {
                var motion = SceneComponents<HouseNpc>().FirstOrDefault(npc => npc.Id == askerId)?.GetComponent<HouseNpcMotion>();
                Assert.Fail("The dialogue never opened on " + askerId + ": misses " + director.AskMissCount(director.CurrentNpcInvitationRecordId)
                    + ", arrival " + motion?.ArrivalFailure + ", route " + motion?.LastRouteFailure);
            }
            Assert.That(director.TalkingToId, Is.EqualTo(askerId));
        }

        private EpisodeHud ComesHud => director.GetComponentInChildren<EpisodeHud>();

        private TMP_Text LastLabel(string name) =>
            director.GetComponentsInChildren<TMP_Text>(true).LastOrDefault(label => label.name == name && label.gameObject.activeInHierarchy);

        private static IEnumerator PinnedClock(Func<IEnumerator> body, float frame = 1f / 30f)
        {
            bool background = Application.runInBackground;
            Application.runInBackground = true;
            Time.captureDeltaTime = frame;
            try { yield return body(); }
            finally { Time.captureDeltaTime = 0f; Application.runInBackground = background; }
        }

        // ------------------------------------------------------------------ 1. the walk-over opens on the offer

        [UnityTest]
        public IEnumerator ComesToYou_ArrivalOpensTheDialogueOnTheExactOffer() => PinnedClock(ArrivalOpensTheDialogue);

        private IEnumerator ArrivalOpensTheDialogue()
        {
            yield return InstallComesToYouHouse(ComesJamie);
            string recordId = director.CurrentNpcInvitationRecordId, askerId = director.CurrentAskerId;
            Assert.That(askerId, Is.EqualTo(ComesMaya));
            var before = director.Snapshot;
            int spent = EpisodeEngine.SocialActionsSpent(before);
            yield return WaitForAskDialogue(askerId);
            // The open frame.
            Assert.That(ComesHud.PointerHeld, Is.True, "Pointer presses are held off the panel as it opens.");
            Assert.That(director.InvitedDealRecordId, Is.EqualTo(recordId));
            Assert.That(director.CurrentAskRecordId, Is.EqualTo(recordId));
            Assert.That(director.CurrentAskerId, Is.EqualTo(askerId));
            Assert.That(director.CurrentConversationCategory, Is.EqualTo(EpisodeDirector.DealsCategoryCaption));
            Assert.That(director.CurrentAskWay, Is.EqualTo(EpisodeDirector.AskWay.WalkOver));
            Assert.That(director.IsAskSetAside(recordId), Is.True);
            yield return null; yield return null;
            Assert.That(SelectedName(), Is.EqualTo(EpisodeHud.ThinkItOverCaption), "The keyboard opens on the way out that answers nothing.");
            yield return WaitForBodies();

            var line = NpcInvitation.Available(director.Snapshot).Single(row => row.RecordId == recordId).OpeningLine;
            Assert.That(LastLabel(EpisodeHud.AskLineName)?.text, Is.EqualTo("\"" + line + "\""));
            Assert.That(ShownText(), Does.Contain(EpisodeHud.AskOfferHeading));
            Assert.That(ShownText(), Does.Contain(EpisodeHud.AskWalkOverEyebrow("Maya")));
            Assert.That(LastLabel(EpisodeHud.AskTermsName)?.text, Is.Not.Null.And.Not.Empty);
            Assert.That(LastLabel(EpisodeHud.AskLapseName)?.text, Is.EqualTo(ComesToYou.LapseNextCampaign));
            foreach (var caption in new[] { EpisodeHud.DealAcceptCaption, EpisodeHud.DealDeclineCaption, EpisodeHud.ThinkItOverCaption,
                         EpisodeDirector.ConversationTopicsBackCaption })
                Assert.That(FindButton(caption), Is.Not.Null, caption); // Single: exactly one of each.

            yield return WaitOutThePointerHold();
            Assert.That(ComesHud.PointerHeld, Is.False);
            var accept = ButtonWithCaption(EpisodeHud.DealAcceptCaption);
            Assert.That(ComesHud.TryDescribeOfferControl(accept, out var described), Is.True);
            Assert.That(described.OfferId, Is.EqualTo(recordId));
            Assert.That(ComesHud.TryReadOfferControl(accept, out var reading), Is.True);
            Assert.That(reading, Does.Contain(director.Snapshot.Find(askerId).name).And.Contain(LastLabel(EpisodeHud.AskTermsName).text));
            Assert.That(NpcDeals.Pending(director.Snapshot).Any(row => row.id == recordId), Is.True, "Hearing the offer does not answer it.");
            Assert.That(EpisodeEngine.SocialActionsSpent(director.Snapshot), Is.EqualTo(spent));

            var justBefore = director.Snapshot;
            accept.onClick.Invoke();
            var answered = director.Snapshot;
            Assert.That(answered.revision, Is.EqualTo(justBefore.revision + 1));
            Assert.That(answered.acceptedCommandIds.Count, Is.EqualTo(justBefore.acceptedCommandIds.Count + 1));
            Assert.That(director.IsConversationExchangePlaying, Is.True);
            var expected = new EpisodeEngine(justBefore).Apply(new EpisodeCommand
            {
                id = answered.acceptedCommandIds.Last(), actorId = justBefore.playerId,
                expectedPhase = justBefore.phase, expectedRevision = justBefore.revision,
                kind = EpisodeCommandKind.RespondToDeal, targetId = recordId, text = EpisodeEngine.AcceptDeal,
            });
            Assert.That(expected.accepted, Is.True, expected.reason);
            AssertEquivalent(expected.state, answered);
        }

        // ------------------------------------------------------------------ 2. Think it over

        [UnityTest]
        public IEnumerator ComesToYou_ThinkItOverCommitsNothingAndIsNotBroughtAgainThisPhase() => PinnedClock(ThinkItOverCommitsNothing);

        private IEnumerator ThinkItOverCommitsNothing()
        {
            yield return InstallComesToYouHouse(ComesJamie);
            string recordId = director.CurrentNpcInvitationRecordId, askerId = director.CurrentAskerId;
            yield return WaitForAskDialogue(askerId);
            Assert.That(ComesHud.PointerHeld, Is.True, "The open frame: pointer presses are held off the panel.");
            yield return WaitOutThePointerHold();
            var think = ButtonWithCaption(EpisodeHud.ThinkItOverCaption);
            var before = director.Snapshot;
            byte[] bytes = File.ReadAllBytes(director.SavePath);
            think.onClick.Invoke();
            // The press frame.
            Assert.That(director.IsPanelOpen, Is.False);
            AssertEquivalent(before, director.Snapshot);
            CollectionAssert.AreEqual(bytes, File.ReadAllBytes(director.SavePath));
            Assert.That(director.IsAskSetAside(recordId), Is.True);
            Assert.That(WaitingOnYou.Read(director.Snapshot).items.Any(item => item.id == recordId), Is.True, "The offer still waits.");

            // Neither gap hides the set-aside: the next scan may run at once, and Maya may come again.
            NpcWrite("askNextAt", NpcRead<float>("askClock"));
            NpcRead<Dictionary<string, float>>("askerNextAt").Remove(askerId);
            float start = NpcRead<float>("askClock");
            float need = EpisodeDirector.AskSettleSeconds + EpisodeDirector.AskStillSeconds + 3 * EpisodeDirector.AskScanSeconds;
            float deadline = Time.realtimeSinceStartup + 15f;
            while (NpcRead<float>("askClock") < start + need && Time.realtimeSinceStartup < deadline)
            {
                Assert.That(director.CurrentNpcInvitationRecordId, Is.Not.EqualTo(recordId), "Not brought again this phase.");
                yield return null;
            }
            Assert.That(NpcRead<float>("askClock"), Is.GreaterThanOrEqualTo(start + need), "The ask clock ran: the wait was not vacuous.");
            Assert.That(director.CurrentNpcInvitationRecordId, Is.Not.EqualTo(recordId));

            // Talking to Maya later this phase opens on the topics wheel, the offer waiting under Deals.
            deadline = Time.realtimeSinceStartup + 5f;
            while (NpcWholeSecondDue() && Time.realtimeSinceStartup < deadline) yield return null;
            var maya = SceneComponents<HouseNpc>().Single(npc => npc.Id == askerId);
            yield return OpenNearbyNpc(maya);
            Assert.That(director.TalkingToId, Is.EqualTo(askerId));
            Assert.That(director.CurrentConversationCategory, Is.Null);
            Assert.That(director.CurrentAskWay, Is.EqualTo(EpisodeDirector.AskWay.None));
            Assert.That(TagOn(FindButton(EpisodeDirector.DealsCategoryCaption)), Does.Contain("waiting"));
            director.ClosePanels();
            yield return null;

            // The control: let the record come again, and the same record leases within the same bound.
            NpcRead<HashSet<string>>("askSetAside").Remove(recordId);
            NpcWrite("askNextAt", NpcRead<float>("askClock"));
            deadline = Time.realtimeSinceStartup + 15f;
            while (director.CurrentNpcInvitationRecordId != recordId && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(director.CurrentNpcInvitationRecordId, Is.EqualTo(recordId), "Without the set-aside the same ask comes again.");
        }

        // ------------------------------------------------------------------ 3. walking away is a retry

        [UnityTest]
        public IEnumerator ComesToYou_WalkingAwayIsARetryNotADeferral() => PinnedClock(WalkingAwayIsARetry);

        /// <summary>A point about 3 m from the player, on the floor, that keeps the player's head well inside the frame.</summary>
        private Vector3 AwayInFrame()
        {
            var view = cameraRig.ViewCamera;
            for (int direction = 0; direction < 16; direction++)
            {
                float angle = direction * Mathf.PI / 8f;
                var candidate = player.transform.position + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * 3f;
                if (!NavMesh.SamplePosition(candidate, out var hit, .5f, player.Agent.areaMask)) continue;
                var seen = view.WorldToViewportPoint(hit.position + Vector3.up * 1.7f);
                if (seen.z > 0f && seen.x > .15f && seen.x < .85f && seen.y > .15f && seen.y < .85f) return hit.position;
            }
            Assert.Fail("No floor 3 m from the player stays in frame.");
            return player.transform.position;
        }

        private IEnumerator WalkingAwayIsARetry()
        {
            yield return InstallComesToYouHouse(ComesJamie);
            string recordId = director.CurrentNpcInvitationRecordId, askerId = director.CurrentAskerId;
            Assert.That(director.NpcInvitationIsVisible, Is.False, "Warped before the asker arrives.");
            WarpPlayer(AwayInFrame());
            float deadline = Time.realtimeSinceStartup + 5f;
            while (director.CurrentNpcInvitationRecordId != null && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(director.CurrentNpcInvitationRecordId, Is.Null, "The visit is let go.");
            Assert.That(director.AskMissCount(recordId), Is.EqualTo(1), "Walking away is a miss.");
            Assert.That(director.IsAskSetAside(recordId), Is.False, "and a retry, not a deferral.");

            deadline = Time.realtimeSinceStartup + EpisodeDirector.AskRetrySeconds + 10f;
            while (director.CurrentNpcInvitationRecordId != recordId && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(director.CurrentNpcInvitationRecordId, Is.EqualTo(recordId), "The same ask comes again after its retry.");
            Assert.That(director.NpcInvitationIsVisible, Is.False);
            WarpPlayer(AwayInFrame());
            deadline = Time.realtimeSinceStartup + 5f;
            while (director.CurrentNpcInvitationRecordId != null && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(director.AskMissCount(recordId), Is.EqualTo(2));
            Assert.That(director.IsAskSetAside(recordId), Is.False);

            // Two misses: no further walk-over this phase. From slice 2 the ask comes as a card where the player stands,
            // once its retry has passed.
            deadline = Time.realtimeSinceStartup + EpisodeDirector.AskRetrySeconds + 20f;
            while (!director.IsAskCardOpen && Time.realtimeSinceStartup < deadline)
            {
                Assert.That(director.CurrentNpcInvitationRecordId, Is.Not.EqualTo(recordId), "Two misses: no further walk-over this phase.");
                yield return null;
            }
            Assert.That(director.IsAskCardOpen, Is.True, "After two misses the ask comes as a card.");
            Assert.That(director.CurrentAskWay, Is.EqualTo(EpisodeDirector.AskWay.Card));
            Assert.That(director.CurrentAskRecordId, Is.EqualTo(recordId));
            Assert.That(director.CurrentAskerId, Is.EqualTo(askerId));
        }

        // ------------------------------------------------------------------ 4. talking to the asker

        [UnityTest]
        public IEnumerator ComesToYou_TalkingToTheAskerOpensOnTheOffer() => PinnedClock(TalkingToTheAsker);

        /// <summary>Waits for the asker's line to be up and no whole second of the house's time due, holding the auto-open back meanwhile.</summary>
        private IEnumerator WaitForTheLineWithNoSecondDue()
        {
            float deadline = Time.realtimeSinceStartup + 35f;
            while ((!director.NpcInvitationIsVisible || NpcWholeSecondDue()) && Time.realtimeSinceStartup < deadline)
            {
                NpcWrite("askSpokeFor", 0f);
                yield return null;
            }
            Assert.That(director.NpcInvitationIsVisible, Is.True);
            Assert.That(NpcWholeSecondDue(), Is.False);
            Assert.That(director.TalkingToId, Is.Null);
        }

        private IEnumerator TalkingToTheAsker()
        {
            yield return InstallComesToYouHouse(ComesJamie);
            string recordId = director.CurrentNpcInvitationRecordId, askerId = director.CurrentAskerId;
            yield return WaitForTheLineWithNoSecondDue();
            byte[] bytes = File.ReadAllBytes(director.SavePath);
            Assert.That(director.TryOpenNpc(askerId), Is.True);
            Assert.That(director.CurrentAskWay, Is.EqualTo(EpisodeDirector.AskWay.Raised));
            Assert.That(director.InvitedDealRecordId, Is.EqualTo(recordId));
            Assert.That(ComesHud.PointerHeld, Is.True);
            CollectionAssert.AreEqual(bytes, File.ReadAllBytes(director.SavePath), "Opening on the offer commits nothing.");
            yield return null; yield return null;
            Assert.That(SelectedName(), Is.EqualTo(EpisodeDirector.ConversationTopicsBackCaption));
            Assert.That(ShownText(), Does.Contain(EpisodeHud.AskRaisedEyebrow("Maya")));
            Assert.That(ButtonWithCaptionOrNull(EpisodeHud.ThinkItOverCaption), Is.Null, "A raised dialogue has no Think it over.");

            yield return WaitOutThePointerHold();
            ButtonWithCaption(EpisodeDirector.ConversationTopicsBackCaption).onClick.Invoke();
            Assert.That(director.CurrentConversationCategory, Is.Null, "Back to topics: the wheel.");
            Assert.That(director.CurrentAskWay, Is.EqualTo(EpisodeDirector.AskWay.None));
            director.ClosePanels();
            yield return null;
            float deadline = Time.realtimeSinceStartup + 5f;
            while (NpcWholeSecondDue() && Time.realtimeSinceStartup < deadline) yield return null;
            yield return OpenNearbyNpc(SceneComponents<HouseNpc>().Single(npc => npc.Id == askerId));
            Assert.That(director.CurrentConversationCategory, Is.Null, "Raised once a phase: the wheel the second time.");
            Assert.That(director.CurrentAskWay, Is.EqualTo(EpisodeDirector.AskWay.None));
        }

        /// <summary>4d: a click on the asker whose line is up hears the ask, the way E does; with asks off, the click's usual route.</summary>
        [UnityTest]
        public IEnumerator ComesToYou_AClickOnTheAskerHearsTheAsk() => PinnedClock(AClickOnTheAsker);

        private IEnumerator AClickOnTheAsker()
        {
            var select = typeof(EpisodeDirector).GetMethod("SelectHouseguest", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(select, Is.Not.Null);
            yield return InstallComesToYouHouse(ComesJamie);
            string recordId = director.CurrentNpcInvitationRecordId, askerId = director.CurrentAskerId;
            yield return WaitForTheLineWithNoSecondDue();
            var asker = SceneComponents<HouseNpc>().Single(npc => npc.Id == askerId);
            byte[] bytes = File.ReadAllBytes(director.SavePath);
            select.Invoke(director, new object[] { asker });
            Assert.That(director.TalkingToId, Is.EqualTo(askerId));
            Assert.That(ComesHud.PointerHeld, Is.True, "The open frame: the click that opened it presses nothing on the panel.");
            Assert.That(director.CurrentAskWay, Is.EqualTo(EpisodeDirector.AskWay.WalkOver), "The click hears the ask it walked over with.");
            Assert.That(NpcRead<Dictionary<string, int>>("askPresentations")[recordId], Is.EqualTo(1), "One presentation counted.");
            CollectionAssert.AreEqual(bytes, File.ReadAllBytes(director.SavePath));
            yield return null; yield return null;
            Assert.That(SelectedName(), Is.EqualTo(EpisodeHud.ThinkItOverCaption));
            Assert.That(ShownText(), Does.Contain(EpisodeHud.AskWalkOverEyebrow("Maya")));
            Assert.That(FindButton(EpisodeHud.ThinkItOverCaption), Is.Not.Null);

            // With asks off the same click takes its usual route: never the ask's dialogue.
            yield return InstallLegacyInvitationHouse();
            Assert.That(director.AsksAreOn, Is.False);
            var visitor = SceneComponents<HouseNpc>().Single(npc => npc.Id == director.CurrentNpcInvitationNpcId);
            select.Invoke(director, new object[] { visitor });
            Assert.That(director.CurrentAskWay, Is.EqualTo(EpisodeDirector.AskWay.None));
            if (director.TalkingToId != null) Assert.That(director.InvitedDealRecordId, Is.Null, "Today's click opens no invited offer.");
        }

        /// <summary>4c: from a houseguest's free-time screen, asking or pitching opens on that category; the ask is not raised or set aside.</summary>
        [UnityTest]
        public IEnumerator ComesToYou_ComingToAskOrPitchOpensOnThatCategory() => PinnedClock(ComingToAskOrPitch);

        private IEnumerator ComingToAskOrPitch()
        {
            yield return InstallComesToYouHouse(ComesJamie);
            string recordId = director.CurrentNpcInvitationRecordId, askerId = director.CurrentAskerId;
            foreach (var (intent, category) in new[] { (EpisodeDirector.IntentDeal, EpisodeDirector.DealsCategoryCaption),
                         (EpisodeDirector.IntentAsk, EpisodeDirector.LearnCategoryCaption) })
            {
                director.TalkWithIntent(askerId, intent);
                float deadline = Time.realtimeSinceStartup + 20f;
                while (director.TalkingToId != askerId && Time.realtimeSinceStartup < deadline) yield return null;
                Assert.That(director.TalkingToId, Is.EqualTo(askerId), intent);
                Assert.That(director.CurrentConversationCategory, Is.EqualTo(category), intent);
                Assert.That(director.CurrentAskWay, Is.EqualTo(EpisodeDirector.AskWay.None), intent + ": no ask dialogue.");
                Assert.That(director.IsAskSetAside(recordId), Is.False, intent + ": the ask is not set aside.");
                if (intent == EpisodeDirector.IntentDeal)
                    Assert.That(ShownText(), Does.Contain("AN OFFER FROM " + director.Snapshot.Find(askerId).name.ToUpperInvariant()),
                        "The Deals page shows her offer first.");
                director.ClosePanels();
                yield return null;
            }
        }

        /// <summary>4b: where the conversation only greets, it draws the notice and raises nothing.</summary>
        [UnityTest]
        public IEnumerator ComesToYou_NoRaiseWhereTheConversationOnlyGreets() => PinnedClock(NoRaiseWhereItOnlyGreets);

        private IEnumerator NoRaiseWhereItOnlyGreets()
        {
            var state = FullHouse(4402, 8);
            var npcs = state.Active.Where(actor => !actor.isPlayer).Select(actor => actor.id).ToList();
            state.phase = EpisodePhase.Nomination;
            state.hohId = npcs[0];
            state.nominees = new List<string>();
            state.dealRulesStartWeek = 1;
            state.deals.Add(new DealState
            {
                id = NpcDeals.OfferPrefix + state.nextSequence++, type = DealKind.Partnership, proposerId = npcs[1],
                recipientId = state.playerId, status = DealStatus.Proposed, week = state.week, expiresWeek = state.week,
                trustImpact = DealKind.DefaultTrust(DealKind.Partnership),
            });
            Assert.That(EpisodeValidation.TryValidate(state, out var reason), Is.True, reason);
            new EpisodeSaveStore(director.SavePath).Save(state);
            yield return ReloadEpisode();
            NpcWrite("proximityWatch", -1e6f);
            yield return WaitForBodiesPairingNobody();
            director.AsksEnabled = true;
            float deadline = Time.realtimeSinceStartup + 2f;
            while (!NpcRead<bool>("asksWereOn") && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(NpcRead<bool>("asksWereOn"), Is.True, "The switch takes effect.");
            var ask = ComesToYou.Read(director.Snapshot).Single(row => row.askerId == npcs[1]);
            deadline = Time.realtimeSinceStartup + 5f;
            while (NpcWholeSecondDue() && Time.realtimeSinceStartup < deadline) yield return null;
            var before = director.Snapshot;
            yield return OpenNearbyNpc(SceneComponents<HouseNpc>().Single(npc => npc.Id == npcs[1]));
            Assert.That(director.TalkingToId, Is.EqualTo(npcs[1]));
            Assert.That(director.CurrentAskWay, Is.EqualTo(EpisodeDirector.AskWay.None), "The notice, not the ask.");
            Assert.That(director.IsAskSetAside(ask.recordId), Is.False);
            Canvas.ForceUpdateCanvases();
            Assert.That(ActiveRect(EpisodeHud.ConversationNoticeName), Is.Not.Null, "The conversation draws the notice card.");
            Assert.That(LastLabel("NPC spoken dialogue")?.text, Does.Contain(HouseDialogue.Greeting(before, npcs[1])),
                "and its line is the greeting.");
            Assert.That(LastLabel(EpisodeHud.AskLineName), Is.Null, "No ask's line on it.");
        }

        // ------------------------------------------------------------------ 5. off in tests and verification

        [UnityTest]
        public IEnumerator ComesToYou_OffInTestsAndVerificationByDefault() => PinnedClock(OffByDefault);

        private IEnumerator OffByDefault()
        {
            Assert.That(director.AsksEnabled, Is.False, "The fixture turns asks off as it binds the director.");
            yield return InstallLegacyInvitationHouse();
            Assert.That(director.AsksEnabled, Is.False, "and again as it binds the reloaded one.");
            Assert.That(director.AsksAreOn, Is.False);
            float until = Time.realtimeSinceStartup + 5f;
            while (Time.realtimeSinceStartup < until)
            {
                Assert.That(director.TalkingToId, Is.Null, "Nothing opens by itself with asks off.");
                yield return null;
            }
            string offerId = director.CurrentNpcInvitationRecordId;
            director.Interact();
            Assert.That(director.InvitedDealRecordId, Is.EqualTo(offerId));
            Assert.That(director.CurrentAskWay, Is.EqualTo(EpisodeDirector.AskWay.None));
            yield return null; yield return null;
            Assert.That(ShownText(), Does.Contain(EpisodeDirector.DealsCategoryCaption.ToUpperInvariant()), "The legacy invited view: its category chrome.");
            Assert.That(ButtonWithCaptionOrNull(EpisodeHud.ThinkItOverCaption), Is.Null);

            director.AsksEnabled = null;
            Assert.That(director.AsksAreOn, Is.EqualTo(!Application.isBatchMode), "The default: on in the editor and the player, off in a batch run.");
            director.AsksEnabled = false;
        }

        // ------------------------------------------------------------------ 6. held in the overview and under the emote menu

        [UnityTest]
        public IEnumerator ComesToYou_HeldInTheOverviewAndUnderTheEmoteMenu() => PinnedClock(HeldUnderChrome);

        private IEnumerator HeldUnderChrome()
        {
            yield return InstallComesToYouHouse(ComesJamie);
            string askerId = director.CurrentAskerId;
            Assert.That(director.ShowOverview(), Is.True);
            yield return HeldThenOpens(askerId, "the overview", () => director.EndOverview());
            director.ClosePanels();
            yield return null;

            // Jamie's turn, under the emote menu.
            var aside = NpcRead<HashSet<string>>("askSetAside");
            foreach (var id in HeldAside(new[] { ComesJamie })) aside.Remove(id);
            NpcWrite("askNextAt", NpcRead<float>("askClock"));
            float deadline = Time.realtimeSinceStartup + 35f;
            while (director.CurrentAskerId != ComesJamie && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(director.CurrentAskerId, Is.EqualTo(ComesJamie));
            director.ToggleEmoteMenu();
            Assert.That(director.EmoteMenuOpen, Is.True);
            yield return HeldThenOpens(ComesJamie, "the emote menu", () => director.ToggleEmoteMenu());
        }

        /// <summary>The asker's line shows and nothing opens for 5 real seconds from the frame it is first up; once the chrome goes, the dialogue opens.</summary>
        private IEnumerator HeldThenOpens(string askerId, string chrome, Action close)
        {
            float deadline = Time.realtimeSinceStartup + 35f;
            while (!director.NpcInvitationIsVisible && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(director.NpcInvitationIsVisible, Is.True, "The line shows under " + chrome + ".");
            float until = Time.realtimeSinceStartup + 5f;
            while (Time.realtimeSinceStartup < until)
            {
                Assert.That(director.TalkingToId, Is.Null, "Nothing opens under " + chrome + ".");
                yield return null;
            }
            close();
            // The lease may run out while held; that is no miss, and the ask comes again after its retry.
            yield return WaitForAskDialogue(askerId, 40f);
            Assert.That(director.CurrentAskWay, Is.EqualTo(EpisodeDirector.AskWay.WalkOver));
        }

        // ------------------------------------------------------------------ 7. a seated asker does not block the next

        [UnityTest]
        public IEnumerator ComesToYou_SeatedAskerDoesNotBlockTheNext() => PinnedClock(SeatedAsker);

        private IEnumerator SeatedAsker()
        {
            yield return OptIntoComesToYou();
            // Jamie's ask is first in public order ("jamie-roberts" before "maya-hassan"): seat Jamie.
            var first = ComesToYou.Read(director.Snapshot).First();
            Assert.That(first.askerId, Is.EqualTo(ComesJamie));
            var coordinator = NpcRead<HouseMeetingCoordinator>("npcMeetings");
            var seat = HouseFurniture.InScene(player.gameObject.scene).FirstOrDefault(anchor => coordinator.ActivityAnchorAvailable(anchor));
            Assert.That(seat, Is.Not.Null, "A free piece of furniture.");
            Assert.That(coordinator.TryReserveActivity(ComesJamie, seat, out var activity, out var reason), Is.True, reason);

            float deadline = Time.realtimeSinceStartup + 35f;
            while (director.CurrentNpcInvitationRecordId == null && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(director.CurrentAskerId, Is.EqualTo(ComesMaya), "The next asker comes while Jamie sits.");
            director.ClosePanels();

            // Jamie's own refusals add up to the card's time, setting nothing aside, and from slice 2 the ask comes as a card
            // where the player stands - on the very scan that makes it due - Jamie staying in their seat.
            var unreachable = NpcRead<Dictionary<string, float>>("askUnreachable");
            NpcRead<HashSet<string>>("askSetAside").UnionWith(HeldAside(new[] { ComesMaya }));
            deadline = Time.realtimeSinceStartup + 40f;
            while ((!unreachable.TryGetValue(first.recordId, out float seconds) || seconds < EpisodeDirector.AskUnreachableSecondsBeforeCard)
                   && !director.IsAskCardOpen && Time.realtimeSinceStartup < deadline)
            {
                Assert.That(director.IsAskSetAside(first.recordId), Is.False, "A busy asker's refusals set nothing aside.");
                if (activity.Released) Assert.That(coordinator.TryReserveActivity(ComesJamie, seat, out activity, out reason), Is.True, reason);
                yield return null;
            }
            Assert.That(unreachable.TryGetValue(first.recordId, out float charged) ? charged : 0f,
                Is.GreaterThanOrEqualTo(EpisodeDirector.AskUnreachableSecondsBeforeCard), "No card before Jamie's own refusals add up to the card's time.");
            // The scan that makes it due opens it, in its place in the order: no scan later, after something listed behind it.
            Assert.That(director.IsAskCardOpen, Is.True, "A busy asker's ask comes as a card on the scan that makes it due.");
            Assert.That(director.CurrentAskWay, Is.EqualTo(EpisodeDirector.AskWay.Card));
            Assert.That(director.CurrentAskRecordId, Is.EqualTo(first.recordId));
            Assert.That(director.CurrentAskerId, Is.EqualTo(ComesJamie));
            coordinator.ReleaseActivity(activity);
        }

        // ------------------------------------------------------------------ 8. no ask while walking, at any frame rate

        [UnityTest]
        public IEnumerator ComesToYou_NoAskWhileWalkingAtHighFrameRate() => PinnedClock(NoAskWhileWalking, 1f / 240f);

        private IEnumerator NoAskWhileWalking()
        {
            yield return OptIntoComesToYou(ComesJamie);
            var from = player.transform.position;
            Vector3 to = from;
            for (int direction = 0; direction < 16 && to == from; direction++)
            {
                float angle = direction * Mathf.PI / 8f;
                var candidate = from + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * 2f;
                if (NavMesh.SamplePosition(candidate, out var hit, .4f, player.Agent.areaMask)
                    && !NavMesh.Raycast(from, hit.position, out _, player.Agent.areaMask)) to = hit.position;
            }
            Assert.That(to, Is.Not.EqualTo(from), "Two floor points in one room.");
            float until = Time.realtimeSinceStartup + 4f;
            bool outbound = true;
            Assert.That(player.TryMoveTo(to), Is.True);
            while (Time.realtimeSinceStartup < until)
            {
                Assert.That(director.CurrentNpcInvitationRecordId, Is.Null, "No visit is reserved while the player walks.");
                if (player.HasArrived)
                {
                    outbound = !outbound;
                    if (Time.realtimeSinceStartup < until) Assert.That(player.TryMoveTo(outbound ? to : from), Is.True);
                }
                yield return null;
            }
            float deadline = Time.realtimeSinceStartup + 10f;
            while (!player.HasArrived && Time.realtimeSinceStartup < deadline) yield return null;
            float arrived = Time.realtimeSinceStartup;
            deadline = arrived + 10f;
            while (director.CurrentNpcInvitationRecordId == null && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(director.CurrentNpcInvitationRecordId, Is.Not.Null, "Once the player stands, a visit is reserved.");
            Assert.That(Time.realtimeSinceStartup - arrived, Is.GreaterThanOrEqualTo(EpisodeDirector.AskStillSeconds - .02f),
                "No sooner than the still time after the arrival.");
        }

        // ------------------------------------------------------------------ 9. the asker is never paired or walked in on

        [UnityTest]
        public IEnumerator ComesToYou_AskerIsNeverPairedOrWalkedInOn() => PinnedClock(AskerNeverPaired);

        /// <summary>
        /// While the asker comes over, while their line is up and across the open, they are in no pending pair and no
        /// approach, and nobody walks in on them. The open is made to carry the house's time up to a pairing scan - the
        /// flush inside TryOpenNpc, after the visit itself has been let go, is where the busy set matters - and the walk-in
        /// is tried on the asker with somebody put beside them, against the same try with asks off.
        /// </summary>
        private IEnumerator AskerNeverPaired()
        {
            yield return InstallComesToYouHouse(ComesJamie);
            // This test runs the walk-in watch.
            NpcWrite("proximityWatch", 0f);
            string askerId = director.CurrentAskerId;
            int walkInsBefore = WalkInsOn(askerId);

            // The line up, no pending pair the open's flush would wait on, and the asker free in the saved world - no
            // pending pair, no cooldown - so only the ask keeps them out of the scan. The auto-open is held meanwhile.
            bool Ready() => director.CurrentAskerId == askerId && director.NpcInvitationIsVisible && director.TalkingToId == null
                && (bool)NpcInvoke("AllPendingNpcLeasesReady") && !NpcPairing.Busy(director.Snapshot).Contains(askerId);
            float deadline = Time.realtimeSinceStartup + 60f;
            while (!Ready() && Time.realtimeSinceStartup < deadline)
            {
                if (director.CurrentNpcInvitationRecordId != null)
                {
                    AssertAskerUnpaired(askerId, "while they bring an ask");
                    Assert.That(new[] { NpcRead<string>("walkInFirst"), NpcRead<string>("walkInSecond") }, Has.No.Member(askerId),
                        "nor walked in on.");
                }
                NpcWrite("askSpokeFor", 0f);
                // The wait may outlast a lease; a miss here is the test's holding the line back, so the asker keeps coming.
                NpcRead<Dictionary<string, int>>("askMisses").Clear();
                yield return null;
            }
            Assert.That(Ready(), Is.True, "The line is up and the house could pair the asker: " + director.NpcAutonomyDiagnostic);
            AssertAskerUnpaired(askerId, "with their line up");

            // Somebody beside the asker is no walk-in while the asker is with the player; with asks off, the same two are.
            var tryWalkIn = typeof(EpisodeDirector).GetMethod("TryWalkInPair", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(tryWalkIn, Is.Not.Null);
            bool WalkIn()
            {
                var arguments = new object[] { new List<string> { askerId, ComesJamie }, null, null };
                return (bool)tryWalkIn.Invoke(director, arguments);
            }
            var asker = SceneComponents<HouseNpc>().Single(npc => npc.Id == askerId);
            var other = SceneComponents<HouseNpc>().Single(npc => npc.Id == ComesJamie);
            Vector3 home = other.transform.position;
            Assert.That(PutBodyAt(other, BesideAsker(asker)), Is.True);
            Physics.SyncTransforms();
            bool withAsks = WalkIn();
            // Read at once, so no frame sees the switch turn: TickAsks never lets the visit go for it.
            director.AsksEnabled = false;
            bool withoutAsks = WalkIn();
            director.AsksEnabled = true;
            PutBodyAt(other, home);
            Physics.SyncTransforms();
            Assert.That(withoutAsks, Is.True, "The control: with asks off, the two of them are walked in on.");
            Assert.That(withAsks, Is.False, "With the asker bringing an ask, nobody walks in on them.");

            // E hears the ask; its open carries the house's time up to the next pairing scan.
            var state = director.Snapshot;
            long clock = state.npcSocial.clockTick, scanAt = state.npcSocial.nextScanTick;
            NpcWrite("npcFrameFraction", (double)Math.Max(1L, scanAt - clock));
            NpcWrite("npcWorldFraction", 0d);
            director.Interact();
            Assert.That(director.TalkingToId, Is.EqualTo(askerId), "E hears the ask.");
            Assert.That(director.CurrentAskWay, Is.EqualTo(EpisodeDirector.AskWay.WalkOver));
            Assert.That(director.Snapshot.npcSocial.clockTick, Is.EqualTo(Math.Max(clock + 1, scanAt)),
                "The open's flush committed the house's time up to the scan.");
            Assert.That(director.Snapshot.npcSocial.nextScanTick, Is.GreaterThan(scanAt), "and the scan came due, so the house planned its pairs.");
            AssertAskerUnpaired(askerId, "across the open");
            yield return null;
            AssertAskerUnpaired(askerId, "once the dialogue is up");
            Assert.That(WalkInsOn(askerId), Is.EqualTo(walkInsBefore), "No walk-in on the asker was committed.");
        }

        /// <summary>The asker is in no pending pair, saved or held, and in no approach under way.</summary>
        private void AssertAskerUnpaired(string askerId, string when)
        {
            var pairs = NpcRead<Dictionary<long, HouseMeetingLease>>("npcPendingWorld");
            Assert.That(pairs.Values.Any(lease => lease.FirstId == askerId || lease.SecondId == askerId), Is.False,
                "The asker is in no pending pair " + when + ".");
            Assert.That(director.Snapshot.npcSocial.pending.Any(row => row.firstId == askerId || row.secondId == askerId), Is.False,
                "nor a saved one " + when + ".");
            foreach (var approach in NpcRead<IList>("npcApproaches"))
            {
                var lease = (HouseMeetingLease)NpcObjectField(approach, "lease");
                Assert.That(lease.FirstId != askerId && lease.SecondId != askerId, Is.True,
                    "nor an approach " + when + ": " + lease.FirstId + " and " + lease.SecondId + ".");
            }
        }

        /// <summary>Walk-ins committed on a houseguest: the situations the walk-in watch has drawn about them.</summary>
        private int WalkInsOn(string id) => director.Snapshot.houseEvents.Count(row => row != null
            && row.kind == HouseEventKind.Proximity && row.involvedIds != null && row.involvedIds.Contains(id));

        /// <summary>A floor point beside the asker, well within a walk-in pair's reach of them and clear of the player.</summary>
        private Vector3 BesideAsker(HouseNpc asker)
        {
            for (int direction = 0; direction < 8; direction++)
            {
                float angle = direction * Mathf.PI / 4f;
                var candidate = asker.transform.position + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * 1.2f;
                if (!NavMesh.SamplePosition(candidate, out var hit, .5f, player.Agent.areaMask)) continue;
                var across = hit.position - asker.transform.position; across.y = 0f;
                var fromPlayer = hit.position - player.transform.position; fromPlayer.y = 0f;
                if (across.magnitude <= EpisodeDirector.WalkInPairMetres - .5f && fromPlayer.magnitude >= .8f) return hit.position;
            }
            Assert.Fail("No floor beside " + asker.DisplayName + ".");
            return asker.transform.position;
        }

        // ------------------------------------------------------------------ also in slice 1: a stalled frame

        /// <summary>
        /// A stall is not time the line was on screen. With Run In Background off, a window out of focus stops the player
        /// loop while the clock runs on; counted whole, coming back would open the dialogue at once. One frame held for
        /// 1.5 s, against the 1.2 s beat, adds at most <see cref="EpisodeDirector.AskFrameAllowanceSeconds"/>; the beat
        /// still runs out once the game's own frames add up.
        /// </summary>
        [UnityTest]
        public IEnumerator ComesToYou_AStalledFrameDoesNotOpenTheDialogue() => PinnedClock(AStalledFrameDoesNotOpen);

        private IEnumerator AStalledFrameDoesNotOpen()
        {
            yield return InstallComesToYouHouse(ComesJamie);
            string recordId = director.CurrentNpcInvitationRecordId, askerId = director.CurrentAskerId;
            float deadline = Time.realtimeSinceStartup + 35f;
            while (!director.NpcInvitationIsVisible && Time.realtimeSinceStartup < deadline)
            {
                NpcWrite("askSpokeFor", 0f);
                yield return null;
            }
            Assert.That(director.NpcInvitationIsVisible, Is.True, "The asker's line is up.");
            NpcWrite("askSpokeFor", 0f);
            System.Threading.Thread.Sleep(1500);
            yield return null;
            yield return null;
            Assert.That(director.TalkingToId, Is.Null,
                "A 1.5 s stall spends at most " + EpisodeDirector.AskFrameAllowanceSeconds + " s of the line's beat.");
            Assert.That(NpcRead<float>("askSpokeFor"), Is.LessThan(EpisodeDirector.AskSpeakBeatSeconds));
            Assert.That(director.AskMissCount(recordId), Is.Zero, "and a stall is no miss.");
            yield return WaitForAskDialogue(askerId);
            Assert.That(director.CurrentAskWay, Is.EqualTo(EpisodeDirector.AskWay.WalkOver), "The beat still runs out on the game's frames.");
        }

        // ------------------------------------------------------------------ 10. captures and the frame's fit

        [UnityTest]
        public IEnumerator ComesToYou_CapturesTheDialogueAtEachSizeAndMotion() => PinnedClock(CapturesTheDialogue);

        private IEnumerator CapturesTheDialogue()
        {
            foreach (var textSize in new[] { TextSize.Standard, TextSize.Largest })
                foreach (bool reduced in new[] { false, true })
                {
                    string at = textSize + (reduced ? ", reduced motion" : "");
                    yield return InstallComesToYouHouse(ComesJamie);
                    // Jamie waits too, so the dialogue draws its "1 more" line: one visit at a time, so Jamie's ask waits
                    // on Maya's, and whichever of them comes first, the other is the one more who wants a word.
                    var aside = NpcRead<HashSet<string>>("askSetAside");
                    foreach (var id in HeldAside(new[] { ComesJamie })) aside.Remove(id);
                    yield return ApplyTextSize(textSize);
                    if (reduced)
                    {
                        director.OpenSettings();
                        yield return null;
                        ButtonWithCaption("Reduce character motion").onClick.Invoke();
                        yield return null;
                        director.ClosePanels();
                        yield return null;
                        Assert.That(cameraRig.ReducedMotion, Is.True, at + ": the setting took, so this pass is not vacuous.");
                    }
                    // Waits for the open, keeping the shot from the frame before it.
                    Vector3 focusBefore = cameraRig.DesiredFocus;
                    float deadline = Time.realtimeSinceStartup + 45f;
                    while (director.TalkingToId == null && Time.realtimeSinceStartup < deadline)
                    {
                        focusBefore = cameraRig.DesiredFocus;
                        yield return null;
                    }
                    Assert.That(director.TalkingToId, Is.Not.Null, at + ": the dialogue opens.");
                    string askerId = director.TalkingToId;
                    Assert.That(director.CurrentAskWay, Is.EqualTo(EpisodeDirector.AskWay.WalkOver), at);
                    Assert.That(director.CurrentAskerId, Is.EqualTo(askerId), at);
                    Assert.That(cameraRig.IsConversationFocused, Is.True, at);
                    var asker = SceneComponents<HouseNpc>().Single(npc => npc.Id == askerId);
                    if (reduced)
                    {
                        Assert.That(Vector3.Distance(cameraRig.DesiredFocus, focusBefore), Is.LessThanOrEqualTo(.01f), at + ": the camera does not move.");
                        var seen = cameraRig.ViewCamera.WorldToViewportPoint(asker.transform.position + Vector3.up * 1.6f);
                        Assert.That(seen.z > 0f && seen.x >= 0f && seen.x <= 1f && seen.y >= 0f && seen.y <= 1f, Is.True, at + ": the asker is in frame.");
                    }
                    else Assert.That(Vector3.Distance(cameraRig.DesiredFocus, focusBefore), Is.GreaterThan(.01f), at + ": the rig takes the two-shot.");
                    yield return WaitForBodies();
                    yield return WaitOutThePointerHold();
                    Assert.That(LastLabel(EpisodeHud.AskMoreName)?.text, Is.EqualTo(EpisodeHud.AskMoreWaitingLine(1)),
                        at + ": the other asker is the one more who wants a word.");
                    AssertEveryLabelDraws(at + ": the ask dialogue");
                    foreach (var name in new[] { EpisodeHud.AskLineName, EpisodeHud.AskStandingName, EpisodeHud.AskTermsName,
                                 EpisodeHud.AskLapseName, EpisodeHud.AskMoreName })
                    {
                        var label = LastLabel(name);
                        Assert.That(label, Is.Not.Null, at + ": " + name + " is drawn.");
                        Canvas.ForceUpdateCanvases();
                        Assert.That(label.rectTransform.rect.height, Is.GreaterThanOrEqualTo(label.fontSize * 1.3f - .5f), at + ": " + name + "'s box.");
                        Assert.That(label.fontSize, Is.GreaterThanOrEqualTo(12f), at + ": " + name + " at 1080p.");
                        Assert.That(label.preferredHeight, Is.LessThanOrEqualTo(label.rectTransform.rect.height + .5f), at + ": " + name + " fits.");
                    }
                    if (textSize == TextSize.Largest)
                        foreach (var caption in new[] { EpisodeHud.DealAcceptCaption, EpisodeHud.DealDeclineCaption,
                                     EpisodeHud.ThinkItOverCaption, EpisodeDirector.ConversationTopicsBackCaption })
                            AssertInsideItsViewport(FindButton(caption), at + ": " + caption);
                    if (Application.isBatchMode)
                        yield return CaptureFraming("comes-to-you-in-person" + (reduced ? "-reduced-motion" : "") + TextScales.Suffix(textSize));
                }
        }

        /// <summary>A control wholly inside its scroll view's viewport, as the dialogue opens - no scrolling.</summary>
        private static void AssertInsideItsViewport(Button button, string what)
        {
            Canvas.ForceUpdateCanvases();
            var scroll = button.GetComponentInParent<ScrollRect>();
            if (scroll == null) return;
            var viewport = scroll.viewport != null ? scroll.viewport : (RectTransform)scroll.transform;
            var outer = new Vector3[4]; var inner = new Vector3[4];
            viewport.GetWorldCorners(outer);
            ((RectTransform)button.transform).GetWorldCorners(inner);
            Assert.That(inner[0].y >= outer[0].y - .5f && inner[1].y <= outer[1].y + .5f, Is.True,
                what + " sits inside the viewport (" + inner[0].y + ".." + inner[1].y + " in " + outer[0].y + ".." + outer[1].y + ").");
        }

        // ================================================================== slice 2: it pops up when they cannot come

        /// <summary>
        /// The invitation flow's house with asks on and walk-overs off, so every ask comes as a card where the player
        /// stands; <paramref name="threeAsks"/> adds Taylor's ask (test 15) and <paramref name="story"/> the story system,
        /// for a staged Pull. Returns as the switch takes effect, before the settle and still times could open a card.
        /// </summary>
        private IEnumerator InstallComesToYouCardHouse(bool threeAsks = false, bool story = false, params string[] holdAsideAskers)
        {
            yield return OptIntoComesToYouHouse(threeAsks, story, holdAsideAskers);
            director.AskWalkOvers = false;
            Assert.That(director.IsAskCardOpen, Is.False, "No card before the settle and still times have run.");
        }

        /// <summary>The saved house again with the story system on and nothing open, so a walk-in can be offered as a Pull (InvitationAndPull's staging).</summary>
        private void SaveWithTheStoryOn()
        {
            var store = new EpisodeSaveStore(director.SavePath);
            Assert.That(store.TryLoad(out var state, out string message), Is.True, message);
            EpisodeEngine.EnableStory(state, state.week);
            Assert.That(EpisodeEngine.OpenStoryBeats(state), Is.Empty, "Nothing open: the only Pull is the walk-in a test stages.");
            Assert.That(EpisodeValidation.TryValidate(state, out string reason), Is.True, reason);
            store.Save(state);
        }

        /// <summary>When <see cref="WaitForAskCardUp"/> first saw the card, in real seconds.</summary>
        private float comesCardSeenAt;

        /// <summary>
        /// Waits for an ask's card, bounded in real seconds, with nobody setting out meanwhile; says why on a miss. On the
        /// open frame: the card's way, no conversation, a panel, the ask set aside and pointer presses held off it. After
        /// 2 frames the keyboard is on Think it over.
        /// </summary>
        private IEnumerator WaitForAskCardUp(string at, float seconds = 45f)
        {
            float deadline = Time.realtimeSinceStartup + seconds;
            while (!director.IsAskCardOpen && Time.realtimeSinceStartup < deadline)
            {
                Assert.That(director.CurrentNpcInvitationRecordId, Is.Null, at + ": with walk-overs off nobody sets out.");
                yield return null;
            }
            comesCardSeenAt = Time.realtimeSinceStartup;
            if (!director.IsAskCardOpen)
                Assert.Fail(at + ": no card opened. Free " + NpcRead<float>("askFreeRun") + " s, chrome-free " + NpcRead<float>("askCardSettle")
                    + " s, still " + NpcRead<float>("askStillFor") + " s, spot " + NpcRead<float>("askSpotBlockedFor") + " s, clock "
                    + NpcRead<float>("askClock") + ", next " + NpcRead<float>("askNextAt") + ", in frame "
                    + NpcRead<bool>("askPlayerInFrame") + ", panel " + director.IsPanelOpen + ". " + director.NpcAutonomyDiagnostic);
            // The open frame.
            Assert.That(ComesHud.PointerHeld, Is.True, at + ": pointer presses are held off the card as it opens.");
            Assert.That(director.CurrentAskWay, Is.EqualTo(EpisodeDirector.AskWay.Card), at);
            Assert.That(director.IsPanelOpen, Is.True, at + ": the card is a panel.");
            Assert.That(director.TalkingToId, Is.Null, at + ": no conversation.");
            Assert.That(director.IsAskSetAside(director.CurrentAskRecordId), Is.True, at + ": set aside for the phase as it opens.");
            yield return null; yield return null;
            Assert.That(SelectedName(), Is.EqualTo(EpisodeHud.ThinkItOverCaption), at + ": the keyboard opens on the way out that answers nothing.");
        }

        /// <summary>Every named label of an ask's page: a box at least 1.3x its font, the font at least 12, the copy inside the box.</summary>
        private void AssertAskLabelsFit(string at, params string[] names)
        {
            Canvas.ForceUpdateCanvases();
            foreach (var name in names)
            {
                var label = LastLabel(name);
                Assert.That(label, Is.Not.Null, at + ": " + name + " is drawn.");
                Assert.That(label.rectTransform.rect.height, Is.GreaterThanOrEqualTo(label.fontSize * 1.3f - .5f), at + ": " + name + "'s box.");
                Assert.That(label.fontSize, Is.GreaterThanOrEqualTo(12f), at + ": " + name + " at 1080p.");
                Assert.That(label.preferredHeight, Is.LessThanOrEqualTo(label.rectTransform.rect.height + .5f), at + ": " + name + " fits.");
            }
        }

        // ------------------------------------------------------------------ 11. the card opens without a walk

        [UnityTest]
        public IEnumerator ComesToYou_CardOpensWithoutAWalkAndAnswersFree() => PinnedClock(CardOpensWithoutAWalk);

        private IEnumerator CardOpensWithoutAWalk()
        {
            yield return InstallComesToYouCardHouse(false, false, ComesJamie);
            string recordId = ComesToYou.Read(director.Snapshot).Single(ask => ask.askerId == ComesMaya).recordId;
            int spent = EpisodeEngine.SocialActionsSpent(director.Snapshot);
            yield return WaitForAskCardUp("the card");
            Assert.That(director.CurrentAskRecordId, Is.EqualTo(recordId), "The card is Maya's exact offer.");
            Assert.That(director.CurrentAskerId, Is.EqualTo(ComesMaya));
            Assert.That(ComesHud.CurrentActivityLayout, Is.EqualTo(EpisodeHud.ActivityLayout.Stage), "The card stands on the Stage layout,");
            Assert.That(cameraRig.IsConversationFocused, Is.False, "and the camera frames no two-shot.");
            yield return WaitForBodies();

            var line = NpcInvitation.Available(director.Snapshot).Single(row => row.RecordId == recordId).OpeningLine;
            Assert.That(LastLabel(EpisodeHud.AskLineName)?.text, Is.EqualTo("\"" + line + "\""));
            Assert.That(ShownText(), Does.Contain(EpisodeHud.AskDirectEyebrow));
            Assert.That(ShownText(), Does.Contain(EpisodeHud.AskOfferHeading));
            Assert.That(LastLabel(EpisodeHud.AskTermsName)?.text, Is.Not.Null.And.Not.Empty);
            Assert.That(LastLabel(EpisodeHud.AskLapseName)?.text, Is.EqualTo(ComesToYou.LapseNextCampaign));
            foreach (var caption in new[] { EpisodeHud.DealAcceptCaption, EpisodeHud.DealDeclineCaption, EpisodeHud.ThinkItOverCaption })
                Assert.That(FindButton(caption), Is.Not.Null, caption); // Single: exactly one of each.
            Assert.That(ButtonWithCaptionOrNull(EpisodeDirector.ConversationTopicsBackCaption), Is.Null, "A card is no conversation: no topics to go back to.");

            yield return WaitOutThePointerHold();
            Assert.That(ComesHud.PointerHeld, Is.False);
            var accept = ButtonWithCaption(EpisodeHud.DealAcceptCaption);
            Assert.That(ComesHud.TryDescribeOfferControl(accept, out var described), Is.True);
            Assert.That(described.OfferId, Is.EqualTo(recordId));
            Assert.That(ComesHud.TryReadOfferControl(accept, out var reading), Is.True);
            Assert.That(reading, Does.Contain(director.Snapshot.Find(ComesMaya).name).And.Contain(LastLabel(EpisodeHud.AskTermsName).text));
            Assert.That(NpcDeals.Pending(director.Snapshot).Any(row => row.id == recordId), Is.True, "Seeing the card does not answer it.");
            Assert.That(EpisodeEngine.SocialActionsSpent(director.Snapshot), Is.EqualTo(spent));

            var justBefore = director.Snapshot;
            accept.onClick.Invoke();
            var answered = director.Snapshot;
            Assert.That(answered.revision, Is.EqualTo(justBefore.revision + 1));
            Assert.That(answered.acceptedCommandIds.Count, Is.EqualTo(justBefore.acceptedCommandIds.Count + 1));
            Assert.That(EpisodeEngine.SocialActionsSpent(answered), Is.EqualTo(spent), "Answering is free.");
            var command = new EpisodeCommand
            {
                id = answered.acceptedCommandIds.Last(), actorId = justBefore.playerId,
                expectedPhase = justBefore.phase, expectedRevision = justBefore.revision,
                kind = EpisodeCommandKind.RespondToDeal, targetId = recordId, text = EpisodeEngine.AcceptDeal,
            };
            // What the conversation would have said for it, from the committed log: the card's reply page says it.
            var said = ConversationExchange.Create(justBefore, answered, command,
                answered.events.Where(entry => entry.sequence >= justBefore.nextSequence).ToList());
            Assert.That(said, Is.Not.Null, "The answer has words of its own.");
            var expected = new EpisodeEngine(justBefore).Apply(command);
            Assert.That(expected.accepted, Is.True, expected.reason);
            AssertEquivalent(expected.state, answered);

            // The reply page: the player's words, hers, and the way back to the house.
            Assert.That(director.IsAskCardOpen, Is.True, "The card stays up for the reply.");
            Assert.That(director.IsConversationExchangePlaying, Is.False, "No conversation plays it.");
            Assert.That(LastLabel(EpisodeHud.AskYourWordsName)?.text, Is.EqualTo("\"" + said.PlayerLine + "\""));
            Assert.That(LastLabel(EpisodeHud.AskReplyName)?.text, Is.EqualTo("\"" + said.NpcLine + "\""));
            Assert.That(ButtonWithCaptionOrNull(EpisodeHud.ThinkItOverCaption), Is.Null, "Nothing is left to think over.");
            // The ask clock stands still under the card: an answered card is a presentation too, so the gap counts from here.
            float clock = NpcRead<float>("askClock");
            ButtonWithCaption(EpisodeDirector.SceneCardDoneCaption).onClick.Invoke();
            Assert.That(director.IsAskCardOpen, Is.False);
            Assert.That(director.IsPanelOpen, Is.False, "Back to the house closes the card.");
            Assert.That(NpcRead<float>("askNextAt"), Is.GreaterThanOrEqualTo(clock + EpisodeDirector.AskGapSeconds - .01f),
                "After an answered card the next ask waits its gap.");
        }

        // ------------------------------------------------------------------ 12. closing the card is Think it over

        [UnityTest]
        public IEnumerator ComesToYou_ClosingTheCardIsThinkItOver() => PinnedClock(ClosingTheCard);

        private IEnumerator ClosingTheCard()
        {
            yield return InstallComesToYouCardHouse(false, false, ComesJamie);
            foreach (bool escape in new[] { true, false })
            {
                string how = escape ? "Escape" : "ClosePanels";
                yield return WaitForAskCardUp(how);
                string recordId = director.CurrentAskRecordId;
                // No whole second of the house's time comes due across the press, so a commit seen would be the card's.
                NpcWrite("npcFrameFraction", 0d);
                NpcWrite("npcWorldFraction", 0d);
                // The ask clock stands still under the card: the gap counts from here.
                float clock = NpcRead<float>("askClock");
                var before = director.Snapshot;
                byte[] bytes = File.ReadAllBytes(director.SavePath);
                if (escape) yield return PressKey(Key.Escape);
                else director.ClosePanels();
                Assert.That(director.IsAskCardOpen, Is.False, how + " closes the card.");
                Assert.That(director.IsPanelOpen, Is.False, how);
                AssertEquivalent(before, director.Snapshot);
                CollectionAssert.AreEqual(bytes, File.ReadAllBytes(director.SavePath), how + " commits nothing.");
                Assert.That(director.IsAskSetAside(recordId), Is.True, how + ": the ask is set aside for the phase, as Think it over sets it.");
                Assert.That(WaitingOnYou.Read(director.Snapshot).items.Any(item => item.id == recordId), Is.True, how + ": the offer still waits.");
                Assert.That(NpcRead<float>("askNextAt"), Is.GreaterThanOrEqualTo(clock + EpisodeDirector.AskGapSeconds - .01f), how + ": the next ask waits its gap.");
                // The same ask brought again, for the other way out.
                NpcRead<HashSet<string>>("askSetAside").Remove(recordId);
                NpcWrite("askNextAt", NpcRead<float>("askClock"));
            }
        }

        // ------------------------------------------------------------------ 13. the card's reply narrates nothing

        [UnityTest]
        public IEnumerator ComesToYou_CardReplyLeaksNoNarration() => PinnedClock(CardReplyLeaksNothing);

        private IEnumerator CardReplyLeaksNothing()
        {
            yield return InstallComesToYouCardHouse(false, false, ComesJamie);
            yield return WaitForAskCardUp("the card");
            yield return WaitOutThePointerHold();
            ButtonWithCaption(EpisodeHud.DealAcceptCaption).onClick.Invoke();
            Assert.That(LastLabel(EpisodeHud.AskReplyName), Is.Not.Null, "The card shows the reply,");
            Assert.That(NpcRead<string>("conversationNarration"), Is.Null, "and narrates nothing a conversation would show next.");
            ButtonWithCaption(EpisodeDirector.SceneCardDoneCaption).onClick.Invoke();
            Assert.That(director.IsPanelOpen, Is.False);
            yield return null;
            float deadline = Time.realtimeSinceStartup + 5f;
            while (NpcWholeSecondDue() && Time.realtimeSinceStartup < deadline) yield return null;
            yield return OpenNearbyNpc(SceneComponents<HouseNpc>().Single(npc => npc.Id == ComesMaya));
            Assert.That(director.TalkingToId, Is.EqualTo(ComesMaya));
            yield return null;
            Assert.That(ShownText(), Does.Not.Contain("WHAT HAPPENED"), "The next conversation opens with nothing of the card's answer.");
        }

        // ------------------------------------------------------------------ 14. never over chrome

        [UnityTest, Timeout(300000)]
        public IEnumerator ComesToYou_NeverOverChrome() => PinnedClock(NeverOverChrome);

        private IEnumerator NeverOverChrome()
        {
            yield return InstallComesToYouCardHouse(false, false, ComesJamie);
            string recordId = ComesToYou.Read(director.Snapshot).Single(ask => ask.askerId == ComesMaya).recordId;
            // Every pass brings Maya's ask again: the caps would stop the fourth.
            NpcWrite("askRecordCap", 100); NpcWrite("askPhaseCap", 100); NpcWrite("askWeekCap", 100);
            // The control: due, with nothing up, the card opens at the next scan.
            yield return HoldTheCardAtItsGap("nothing up", recordId);
            NpcWrite("askNextAt", NpcRead<float>("askClock"));
            yield return WaitForAskCardUp("nothing up", EpisodeDirector.AskScanSeconds + 2f);
            director.ClosePanels();
            yield return null;

            var home = player.transform.position;
            yield return CardHeldUnder("the settings", recordId, () => director.OpenSettings(), () => director.IsPanelOpen,
                () => director.ClosePanels(), true);
            yield return CardHeldUnder("the notebook", recordId, () => director.OpenJournal(), () => director.IsPanelOpen,
                () => director.ClosePanels(), true);
            yield return CardHeldUnder("the episode screen", recordId,
                () => { WarpPlayer(director.StationPosition); Assert.That(director.TryOpenPhasePanel(), Is.True); },
                () => director.IsPhasePanelOpen, () => { director.ClosePanels(); WarpPlayer(home); }, true);
            yield return CardHeldUnder("the emote menu", recordId, () => director.ToggleEmoteMenu(), () => director.EmoteMenuOpen,
                () => director.ToggleEmoteMenu(), true);
            // The map-only overview holds no new visit: the free-house clock and the scan run beneath it, so only the card's
            // own gate holds the card there, and the card waits its settle time once the overview has closed.
            yield return CardHeldUnder("the overview", recordId, () => Assert.That(director.ShowOverview(), Is.True), () => director.IsOverview,
                () => director.EndOverview(), true, scanMustRun: true);

            // A staged Pull, on the same house with the story on (InvitationAndPull's staging): a walk-in found as the
            // proximity watch finds one, which writes nothing.
            yield return InstallComesToYouCardHouse(false, true, ComesJamie);
            recordId = ComesToYou.Read(director.Snapshot).Single(ask => ask.askerId == ComesMaya).recordId;
            Assert.That(director.IsOverview || director.IsBriefing, Is.False, "The house's own view, where the Pull's card is built.");
            var now = director.Snapshot;
            Assert.That(EpisodeEngine.StoryOn(now), Is.True, "A walk-in is offered as a Pull only under the story system.");
            var pair = now.Active.Where(actor => !actor.isPlayer && actor.id != ComesMaya).Select(actor => actor.id)
                .OrderBy(id => id, StringComparer.Ordinal).Take(2).ToList();
            Assert.That(pair, Has.Count.EqualTo(2), "Two others to walk in on.");
            yield return CardHeldUnder("a staged Pull", recordId, () =>
                {
                    NpcWrite("walkInFirst", pair[0]);
                    NpcWrite("walkInSecond", pair[1]);
                    NpcWrite("walkInRoom", "Living");
                    NpcWrite("walkInWeek", now.week);
                    NpcWrite("proximityWatch", -1e6f);
                }, () => ActiveRect(EpisodeHud.PullCardName) != null,
                () => FindButton(EpisodeHud.StayOutCaption).onClick.Invoke(), true);
        }

        /// <summary>The house brought to the moment the card would open: the ask free to come again and the settle and still times run, only its gap holding it.</summary>
        private IEnumerator HoldTheCardAtItsGap(string what, string recordId)
        {
            NpcRead<HashSet<string>>("askSetAside").Remove(recordId);
            NpcWrite("askNextAt", float.MaxValue);
            float deadline = Time.realtimeSinceStartup + 15f;
            while ((NpcRead<float>("askFreeRun") < EpisodeDirector.AskSettleSeconds || NpcRead<float>("askStillFor") < EpisodeDirector.AskStillSeconds)
                   && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(NpcRead<float>("askFreeRun"), Is.GreaterThanOrEqualTo(EpisodeDirector.AskSettleSeconds), what + ": the house has settled.");
            Assert.That(NpcRead<float>("askStillFor"), Is.GreaterThanOrEqualTo(EpisodeDirector.AskStillSeconds), what + ": the player stands still.");
            Assert.That(director.IsAskCardOpen, Is.False, what);
        }

        /// <summary>
        /// The card due but for its gap, then the chrome up and the gap let go: for 30 frames, and at least the settle,
        /// still and two scans' time in real seconds, no card. Once the chrome closes the card opens - after a panel, or
        /// anything else that holds the house, only once the settle time has run again. With
        /// <paramref name="scanMustRun"/>, the house stays free under the chrome - the player in frame, the free-house
        /// clock settled, the scan running and reaching the card - so only the card's own gate holds it.
        /// </summary>
        private IEnumerator CardHeldUnder(string what, string recordId, Action open, Func<bool> up, Action close, bool settlesAgain,
            bool scanMustRun = false)
        {
            yield return HoldTheCardAtItsGap(what, recordId);
            open();
            float deadline = Time.realtimeSinceStartup + 5f;
            while (!up() && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(up(), Is.True, what + " is up.");
            // Only the chrome holds the card now.
            NpcWrite("askNextAt", NpcRead<float>("askClock"));
            float scanWas = NpcRead<float>("askScanAt");
            int frames = 0;
            float until = Time.realtimeSinceStartup + EpisodeDirector.AskSettleSeconds + EpisodeDirector.AskStillSeconds
                + 2f * EpisodeDirector.AskScanSeconds;
            while (frames < 30 || Time.realtimeSinceStartup < until)
            {
                yield return null;
                frames++;
                Assert.That(director.IsAskCardOpen, Is.False, "No card over " + what + " (frame " + frames + ").");
                Assert.That(up(), Is.True, what + " stays up while it is watched (frame " + frames + ").");
                if (scanMustRun)
                    Assert.That(NpcRead<bool>("askPlayerInFrame"), Is.True, "Under " + what + " the player stays in frame (frame " + frames + ").");
            }
            if (scanMustRun)
            {
                Assert.That(NpcRead<float>("askFreeRun"), Is.GreaterThanOrEqualTo(EpisodeDirector.AskSettleSeconds), "Under " + what + " the house stays free.");
                Assert.That(NpcRead<float>("askScanAt"), Is.GreaterThan(scanWas), "Under " + what + " the scan runs and reaches the card.");
            }
            close();
            float closed = Time.realtimeSinceStartup;
            yield return WaitForAskCardUp("once " + what + " has closed", 20f);
            Assert.That(director.CurrentAskRecordId, Is.EqualTo(recordId), what);
            if (settlesAgain)
                Assert.That(comesCardSeenAt - closed, Is.GreaterThanOrEqualTo(EpisodeDirector.AskSettleSeconds - EpisodeDirector.AskFrameAllowanceSeconds),
                    "After " + what + " the card waits the settle time again.");
            director.ClosePanels();
            yield return null;
        }

        // ------------------------------------------------------------------ 15. several asks, one at a time, in public order

        [UnityTest, Timeout(300000)]
        public IEnumerator ComesToYou_SeveralAsksOneAtATimeInPublicOrder() => PinnedClock(SeveralAsksInOrder);

        private IEnumerator SeveralAsksInOrder()
        {
            // Three houseguests ask, so three cards: one at a time, in the queue's public order, a gap apart on the free
            // house's clock, each saying how many others still want a word.
            yield return InstallComesToYouCardHouse(threeAsks: true);
            var queue = ComesToYou.Read(director.Snapshot);
            Assert.That(queue.Select(ask => ask.askerId).Distinct().Count(), Is.EqualTo(3),
                "Three houseguests ask: " + string.Join(", ", queue.Select(ask => ask.askerId + "/" + ask.type)));
            var order = queue.Select(ask => ask.recordId).ToList();
            float openedAt = float.NaN;
            for (int i = 0; i < 3; i++)
            {
                string at = "card " + (i + 1);
                yield return WaitForAskCardUp(at, 60f);
                Assert.That(director.CurrentAskRecordId, Is.EqualTo(order[i]), at + " is the " + (i + 1) + ". in public order.");
                // The clock stands still under the card: this is when it opened.
                float clock = NpcRead<float>("askClock");
                if (i > 0) Assert.That(clock - openedAt, Is.GreaterThanOrEqualTo(EpisodeDirector.AskGapSeconds - .01f), at + " came a gap after the last.");
                openedAt = clock;
                if (i < 2) Assert.That(LastLabel(EpisodeHud.AskMoreName)?.text, Is.EqualTo(EpisodeHud.AskMoreWaitingLine(2 - i)), at + ": who else wants a word.");
                else Assert.That(LastLabel(EpisodeHud.AskMoreName), Is.Null, at + ": nobody else waits.");
                yield return WaitOutThePointerHold();
                ButtonWithCaption(EpisodeHud.ThinkItOverCaption).onClick.Invoke();
                Assert.That(director.IsAskCardOpen, Is.False, at + ": Think it over closes the card.");
                Assert.That(director.IsAskSetAside(order[i]), Is.True, at);
            }

            // With the phase's cap at 2, the third asker's card does not open this phase.
            yield return InstallComesToYouCardHouse(threeAsks: true);
            NpcWrite("askPhaseCap", 2);
            order = ComesToYou.Read(director.Snapshot).Select(ask => ask.recordId).ToList();
            for (int i = 0; i < 2; i++)
            {
                string at = "capped card " + (i + 1);
                yield return WaitForAskCardUp(at, 45f);
                Assert.That(director.CurrentAskRecordId, Is.EqualTo(order[i]), at);
                if (i == 1) Assert.That(LastLabel(EpisodeHud.AskMoreName), Is.Null, at + ": the phase's cap is reached, so nobody else comes this phase.");
                yield return WaitOutThePointerHold();
                ButtonWithCaption(EpisodeHud.ThinkItOverCaption).onClick.Invoke();
                // The gap is the first half's subject: the next scan may run at once.
                NpcWrite("askNextAt", NpcRead<float>("askClock"));
            }
            float start = NpcRead<float>("askClock");
            float need = EpisodeDirector.AskSettleSeconds + EpisodeDirector.AskStillSeconds + 3 * EpisodeDirector.AskScanSeconds;
            float until = Time.realtimeSinceStartup + 20f;
            while (NpcRead<float>("askClock") < start + need && Time.realtimeSinceStartup < until)
            {
                Assert.That(director.IsAskCardOpen, Is.False, "The third asker's card does not open in this phase.");
                yield return null;
            }
            Assert.That(NpcRead<float>("askClock"), Is.GreaterThanOrEqualTo(start + need), "The ask clock ran: the wait was not vacuous.");
            Assert.That(director.IsAskCardOpen, Is.False);
            // The phase moves on. A real Advance from this house's free time reaches the Head of Household competition,
            // where no ask comes (NpcInvitation.PhaseAllows), and then the nominations, where without the week's windows
            // only those deciding can talk; so the ask model's own phase key is moved on, which is what its caps count
            // against. The two asks already put once stay out under a cap of one each, so only the third can come.
            NpcWrite("askRecordCap", 1);
            NpcWrite("askPhase", EpisodePhase.HoH);
            yield return WaitForAskCardUp("after the phase moves on", 20f);
            Assert.That(director.CurrentAskRecordId, Is.EqualTo(order[2]), "The third asker's card opens in the next phase.");
            Assert.That(NpcRead<int>("askPhaseShown"), Is.EqualTo(1), "The phase's count started again.");
        }

        // ------------------------------------------------------------------ 16. captures of the card

        [UnityTest, Timeout(300000)]
        public IEnumerator ComesToYou_CapturesTheCardAtEachSize() => PinnedClock(CapturesTheCard);

        private IEnumerator CapturesTheCard()
        {
            foreach (var textSize in new[] { TextSize.Standard, TextSize.Largest })
            {
                string at = textSize.ToString();
                // Nobody held aside: one card at a time, so the second asker is the one more who wants a word.
                yield return InstallComesToYouCardHouse();
                string first = ComesToYou.Read(director.Snapshot).First().askerId;
                yield return ApplyTextSize(textSize);
                yield return WaitForAskCardUp(at);
                Assert.That(director.CurrentAskerId, Is.EqualTo(first), at + ": the first in public order.");
                Assert.That(ComesHud.CurrentActivityLayout, Is.EqualTo(EpisodeHud.ActivityLayout.Stage), at);
                yield return WaitForBodies();
                yield return WaitOutThePointerHold();
                Assert.That(LastLabel(EpisodeHud.AskMoreName)?.text, Is.EqualTo(EpisodeHud.AskMoreWaitingLine(1)),
                    at + ": the other asker is the one more who wants a word.");
                AssertEveryLabelDraws(at + ": the ask card");
                AssertAskLabelsFit(at + ": the ask card", EpisodeHud.AskLineName, EpisodeHud.AskStandingName, EpisodeHud.AskTermsName,
                    EpisodeHud.AskLapseName, EpisodeHud.AskMoreName);
                if (textSize == TextSize.Largest)
                    foreach (var caption in new[] { EpisodeHud.DealAcceptCaption, EpisodeHud.DealDeclineCaption, EpisodeHud.ThinkItOverCaption })
                        AssertInsideItsViewport(FindButton(caption), at + ": " + caption);
                if (Application.isBatchMode) yield return CaptureFraming("comes-to-you-card" + TextScales.Suffix(textSize));

                // The reply page's two lines, at the same size. Test 11 presses as a player does; here only the page matters.
                Assert.That(director.IsAskCardOpen, Is.True, at + ": the card is still up for its answer.");
                FindButton(EpisodeHud.DealAcceptCaption).onClick.Invoke();
                yield return null;
                Assert.That(FindButton(EpisodeDirector.SceneCardDoneCaption), Is.Not.Null, at + ": the reply page's way back.");
                AssertEveryLabelDraws(at + ": the card's reply");
                AssertAskLabelsFit(at + ": the card's reply", EpisodeHud.AskYourWordsName, EpisodeHud.AskReplyName);
                director.ClosePanels();
                yield return null;
            }
        }

        // ------------------------------------------------------------------ 17. a spot that fits no visitor

        [UnityTest, Timeout(300000)]
        public IEnumerator ComesToYou_ASpotThatFitsNoVisitorBringsTheFirstAskAsACard() => PinnedClock(ASpotThatFitsNoVisitor);

        /// <summary>
        /// The player stands still where no visitor can stand beside them: the spot is charged to no ask - nothing missed,
        /// out of reach or set aside, nobody setting out - until it has refused visitors for the card's time, when the
        /// first listed ask comes as a card. Back on open floor the spot's count starts again and the next ask is walked
        /// over: the spot made no other ask card-due. Nobody pairs off or takes a seat meanwhile, so every refusal is the
        /// spot's and never an asker's own.
        /// </summary>
        private IEnumerator ASpotThatFitsNoVisitor()
        {
            yield return OptIntoComesToYou();
            NpcWrite("npcApproachDiagnosticsSuppressed", true);
            float ambient = NpcRead<float>("nextAmbientActivity");
            NpcWrite("nextAmbientActivity", float.PositiveInfinity);
            try { yield return SpotThatFitsNoVisitor(); }
            finally
            {
                NpcWrite("npcApproachDiagnosticsSuppressed", false);
                NpcWrite("nextAmbientActivity", ambient);
            }
        }

        private IEnumerator SpotThatFitsNoVisitor()
        {
            var queue = ComesToYou.Read(director.Snapshot);
            var first = queue.First();
            Assert.That(first.askerId, Is.EqualTo(ComesJamie), "Jamie's ask is listed first.");
            var second = queue.First(ask => ask.askerId == ComesMaya);
            string[] askers = { ComesJamie, ComesMaya };
            var coordinator = NpcRead<HouseMeetingCoordinator>("npcMeetings");
            var home = player.transform.position;

            // The scan held while the player moves there and both askers are seen free, so no wait of the test's is charged.
            NpcWrite("askNextAt", float.MaxValue);
            WarpPlayer(SpotNoVisitorFits(coordinator));
            var refusals = new Dictionary<string, HouseNpcInvitationRefusal>();
            float deadline = Time.realtimeSinceStartup + 10f;
            while (true)
            {
                foreach (var asker in askers)
                {
                    Assert.That(coordinator.TryReserveNpcInvitation(asker, player, out _, out _, out var refusal), Is.False,
                        asker + ": no visitor fits the spot.");
                    refusals[asker] = refusal;
                }
                if (refusals.Values.All(kind => kind == HouseNpcInvitationRefusal.PlayerSpot) || Time.realtimeSinceStartup >= deadline) break;
                yield return null;
            }
            foreach (var asker in askers)
                Assert.That(refusals[asker], Is.EqualTo(HouseNpcInvitationRefusal.PlayerSpot), asker + " is refused for the player's spot, not for their own reasons.");
            var unreachable = NpcRead<Dictionary<string, float>>("askUnreachable");
            Assert.That(unreachable.Values.Where(seconds => seconds > 0f), Is.Empty, "Nothing charged before the scan runs.");
            NpcWrite("askNextAt", NpcRead<float>("askClock"));

            // Charged to nobody until the spot has refused visitors for the card's time.
            deadline = Time.realtimeSinceStartup + EpisodeDirector.AskSettleSeconds + EpisodeDirector.AskStillSeconds
                + EpisodeDirector.AskUnreachableSecondsBeforeCard + 10f;
            while (!director.IsAskCardOpen && Time.realtimeSinceStartup < deadline)
            {
                Assert.That(director.CurrentNpcInvitationRecordId, Is.Null, "Nobody sets out to a spot no visitor fits.");
                Assert.That(unreachable.Values.Where(seconds => seconds > 0f), Is.Empty, "The spot is charged to no asker.");
                foreach (var ask in new[] { first, second })
                {
                    Assert.That(director.AskMissCount(ask.recordId), Is.Zero, ask.askerId + ": no miss.");
                    Assert.That(director.IsAskSetAside(ask.recordId), Is.False, ask.askerId + ": nothing set aside.");
                }
                yield return null;
            }
            float spot = NpcRead<float>("askSpotBlockedFor");
            if (!director.IsAskCardOpen)
                Assert.Fail("The spot's card never opened: spot " + spot + " s, free " + NpcRead<float>("askFreeRun") + " s, still "
                    + NpcRead<float>("askStillFor") + " s, in frame " + NpcRead<bool>("askPlayerInFrame") + ". " + director.NpcAutonomyDiagnostic);
            Assert.That(spot, Is.GreaterThanOrEqualTo(EpisodeDirector.AskUnreachableSecondsBeforeCard), "No card before the spot has held for the card's time.");
            Assert.That(director.CurrentAskRecordId, Is.EqualTo(first.recordId), "The first listed ask comes as the card.");
            Assert.That(director.CurrentAskerId, Is.EqualTo(ComesJamie));
            Assert.That(unreachable.Values.Where(seconds => seconds > 0f), Is.Empty, "Still charged to nobody.");
            Assert.That(director.IsAskSetAside(second.recordId), Is.False, "Only the card's ask is set aside.");
            yield return WaitForAskCardUp("the spot's card", 1f);

            // Back on open floor: the spot's count starts again with the stillness, and the next ask is walked over.
            director.ClosePanels();
            WarpPlayer(home);
            yield return null;
            Assert.That(NpcRead<float>("askSpotBlockedFor"), Is.Zero, "The spot's count starts again where the player stands now.");
            NpcWrite("askNextAt", NpcRead<float>("askClock"));
            deadline = Time.realtimeSinceStartup + EpisodeDirector.AskSettleSeconds + EpisodeDirector.AskStillSeconds
                + 2f * EpisodeDirector.AskScanSeconds + 3f;
            while (director.CurrentNpcInvitationRecordId == null && !director.IsAskCardOpen && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(director.IsAskCardOpen, Is.False, "The spot made no other ask card-due.");
            Assert.That(director.CurrentNpcInvitationRecordId, Is.EqualTo(second.recordId), "Maya sets out to the player's open floor. " + director.NpcAutonomyDiagnostic);
            Assert.That(director.CurrentAskerId, Is.EqualTo(ComesMaya));
            Assert.That(director.CurrentAskWay, Is.EqualTo(EpisodeDirector.AskWay.None), "A walk-over on its way.");
            Assert.That(NpcRead<float>("askSpotBlockedFor"), Is.Zero);
        }

        /// <summary>
        /// The floor point nearest the player, their head well inside the frame, that lies in no room's safe interior - a
        /// doorway, a floor's edge - so the coordinator refuses every visitor for the player's spot before it looks for a
        /// point beside them (HouseMeetingCoordinator.TryReserveNpcInvitation's PlayerSpot).
        /// </summary>
        private Vector3 SpotNoVisitorFits(HouseMeetingCoordinator coordinator)
        {
            var field = typeof(HouseMeetingCoordinator).GetField("rooms", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, "The coordinator's room query.");
            var rooms = (HouseRoomQuery)field.GetValue(coordinator);
            var view = cameraRig.ViewCamera;
            var from = player.transform.position;
            float radius = player.Agent.radius, best = float.PositiveInfinity;
            Vector3 found = from;
            for (float x = -14f; x <= 14f; x += .2f)
                for (float z = -14f; z <= 14f; z += .2f)
                {
                    var candidate = from + new Vector3(x, 0f, z);
                    float distance = new Vector2(x, z).sqrMagnitude;
                    if (distance >= best || !NavMesh.SamplePosition(candidate, out var hit, .1f, player.Agent.areaMask)
                        || Mathf.Abs(hit.position.y - from.y) > .2f || rooms.TryLocate(hit.position, radius, out _)) continue;
                    var seen = view.WorldToViewportPoint(hit.position + Vector3.up * 1.7f);
                    if (seen.z <= 0f || seen.x <= .15f || seen.x >= .85f || seen.y <= .15f || seen.y >= .85f) continue;
                    best = distance;
                    found = hit.position;
                }
            Assert.That(best, Is.LessThan(float.PositiveInfinity), "A floor point in frame that fits no visitor: a doorway or a floor's edge.");
            return found;
        }
    }
}
