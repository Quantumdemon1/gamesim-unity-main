using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Gamesim.Episode;
using Gamesim.House;
using Gamesim.Presentation;
using Gamesim.Simulation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.TestTools;

namespace Gamesim.Tests.PlayMode
{
    /// <summary>
    /// The player Head of Household's pitchers wait in or by the HoH suite (plan HR-2). Before the names are said, every
    /// houseguest whose pitch the walk-ups would bring goes up and stands in the suite - or by it, when it has no clear spot
    /// left - off its furniture and clear of each other; going up to one there and clicking them opens the pitches the way
    /// OF-p's walk-up does (the board under the role moments, the one-at-a-time answer before), never a conversation, and the
    /// answer there sends the command it sends today. An answered pitcher, and every pitcher once the names are said, goes
    /// back to the stroll; a pitcher's own walk-up takes them from the suite and gives them back; with the reply cards' asks
    /// off nobody is placed. The "Invite them up to the HoH room" act is not touched.
    ///
    /// <para>Every test installs offers slice 4p's pitch house (InstallComesToYouPitchHouse: the house past free time with its
    /// world built, asks and the reply cards' switch set explicitly, the house's pairing held) and pins scaled time
    /// (PinnedClock); the board's season writes story rules Roles by name, never leaning on StoryRules.Current. With walk-overs
    /// off a pitch has no card, so nobody walks up and the suite is the only way the pitchers move.</para>
    /// </summary>
    public sealed partial class EpisodePlayModeTests
    {
        private static readonly MethodInfo SuiteSelect =
            typeof(EpisodeDirector).GetMethod("SelectHouseguest", BindingFlags.Instance | BindingFlags.NonPublic);

        /// <summary>The houseguests behind the pitches the pitch house left waiting, in the board's order.</summary>
        private string[] SuitePitcherOrder() => comesPitchIds.Select(id => ComesPitch(id).fromId).ToArray();

        /// <summary>
        /// Waits, a frame at a time, until every one of these pitchers stands at their spot (<see cref="EpisodeDirector.SuitePitcherStandsThere"/>):
        /// at least the HR-1 trip's frame bound twice over and ten real seconds, so a trip's real-time retry gets its turn.
        /// </summary>
        private IEnumerator SuitePitchersArrive(IReadOnlyCollection<string> pitchers)
        {
            int frames = 0;
            float start = Time.realtimeSinceStartup;
            while (!pitchers.All(director.SuitePitcherStandsThere)
                && (frames < 2 * SuiteTripFrameBound || Time.realtimeSinceStartup - start < 10f))
            {
                frames++;
                yield return null;
            }
            var missing = pitchers.Where(id => !director.SuitePitcherStandsThere(id)).ToList();
            Assert.That(missing, Is.Empty, "Every pitcher stands at their spot by the suite after " + frames + " frames: "
                + string.Join(", ", missing.Select(id => id + (director.SuitePitcherTripForDiagnostics(id) ? " (on the way)" : " (no trip)"))));
            TestContext.Out.WriteLine("HoH suite pitchers: " + pitchers.Count + " standing after " + frames + " frames = "
                + (frames * SuiteFrameSeconds).ToString("0.0") + " s of game time.");
        }

        /// <summary>Whether a body stands in the suite, or by it: within SuiteByMetres of one of its markers and not in the diary room.</summary>
        private static bool InOrBySuite(Transform body)
        {
            string room = RoomAt(body);
            if (room == EpisodeDirector.SuiteRoom) return true;
            return room != "Private" && ActiveRoomMarkers().Where(marker => marker.RoomName == EpisodeDirector.SuiteRoom)
                .Any(marker => FlatDistance(marker.transform.position, body.position) <= EpisodeDirector.SuiteByMetres + .5f);
        }

        /// <summary>Puts the player on the floor beside a houseguest, within talking reach of them; nothing opens.</summary>
        private void StandBeside(HouseNpc npc)
        {
            var canTalk = typeof(EpisodeDirector).GetMethod("CanTalk", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(canTalk, Is.Not.Null);
            for (int direction = 0; direction < 16; direction++)
            {
                float angle = direction * Mathf.PI / 8f, reach = direction < 8 ? 1.5f : 2f;
                var candidate = npc.transform.position + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * reach;
                if (!NavMesh.SamplePosition(candidate, out var hit, .5f, player.Agent.areaMask)) continue;
                if (!player.Agent.Warp(hit.position)) continue;
                player.Agent.ResetPath();
                Physics.SyncTransforms();
                if ((bool)canTalk.Invoke(director, new object[] { npc })) return;
            }
            Assert.Fail("No floor within talking reach of " + npc.DisplayName + ".");
        }

        // ------------------------------------------------------------------ 1. they wait in or by the suite, standing

        [UnityTest, Timeout(300000)]
        public IEnumerator HoHSuite_PitchersWaitInOrByTheSuiteStanding() => PinnedClock(PitchersWaitHeld);

        private IEnumerator PitchersWaitHeld() => WithTheHouseHeld(() => InstallComesToYouPitchHouse(true, false), PitchersWait);

        private IEnumerator PitchersWait()
        {
            var state = director.Snapshot;
            var pitchers = SuitePitcherOrder();
            Assert.That(EpisodeDirector.SuitePitcherIds(state), Is.EqualTo(pitchers), "The board's pitchers, in its order, wait on the player.");
            Assert.That(EpisodeDirector.SuiteHeadId(state), Is.Null, "The player is Head of Household: no Head is placed.");
            yield return SuitePitchersArrive(pitchers);
            Assert.That(director.SuitePitchersForDiagnostics, Is.EquivalentTo(pitchers), "The suite holds the pitchers and nobody else.");

            var house = SuiteHouse();
            var bodies = pitchers.Select(SuiteBody).ToArray();
            for (int i = 0; i < bodies.Length; i++)
            {
                var body = bodies[i];
                Assert.That(body, Is.Not.Null, pitchers[i]);
                Assert.That(InOrBySuite(body.transform), Is.True, pitchers[i] + " waits in or by the suite (in the " + RoomAt(body.transform) + ").");
                Assert.That(SuiteSeated(body), Is.False, pitchers[i] + " stands,");
                Assert.That(house.TryGetActivity(pitchers[i], out _), Is.False, "uses nothing,");
                Assert.That(SuiteInFurniture(body.transform), Is.False, "and is clear of the furniture.");
                for (int j = 0; j < i; j++)
                    Assert.That(FlatDistance(body.transform.position, bodies[j].transform.position), Is.GreaterThan(.6f),
                        pitchers[i] + " and " + pitchers[j] + " stand apart.");
            }

            // The stroll looks every frame and is told everybody is rested: it never takes a pitcher.
            var leases = bodies.ToDictionary(body => body.Id, body => body.GetComponent<HouseNpcMotion>().LeaseId);
            var rest = NpcRead<Dictionary<string, float>>("wanderRestUntil");
            for (int frame = 0; frame < 120; frame++)
            {
                NpcWrite("nextWanderCheck", 0f);
                foreach (string id in rest.Keys.ToList()) rest[id] = 0f;
                yield return null;
                foreach (var body in bodies)
                    Assert.That(body.GetComponent<HouseNpcMotion>().LeaseId, Is.EqualTo(leases[body.Id]), "The stroll never sends " + body.Id + " anywhere (frame " + frame + ").");
            }
            Assert.That(director.Snapshot.replyCards.Select(card => card.id), Is.EqualTo(comesPitchIds), "Waiting answers nothing.");
            Assert.That(director.CurrentNpcInvitationRecordId, Is.Null, "With walk-overs off nobody walks up.");
        }

        // ------------------------------------------------------------------ 2. go up, click: the board, and the answer it sends today

        [UnityTest, Timeout(300000)]
        public IEnumerator HoHSuite_PitchClickedInTheSuiteOpensTheBoardAndAnswersAsToday() => PinnedClock(PitchClickedHeld);

        private IEnumerator PitchClickedHeld() => WithTheHouseHeld(() => InstallComesToYouPitchHouse(true, false), PitchClicked);

        private IEnumerator PitchClicked()
        {
            Assert.That(SuiteSelect, Is.Not.Null);
            var pitchers = SuitePitcherOrder();
            yield return SuitePitchersArrive(pitchers);
            // The second on the board, so the click is shown to open the board and not only the pitch listed first.
            string pitchId = comesPitchIds[1], pitcher = pitchers[1];
            var body = SuiteBody(pitcher);

            // From the room farthest from the suite the click walks the player up, straight to them, never to a talk spot.
            var capsule = player.GetComponent<CapsuleCollider>();
            WarpPlayer(SuiteFarthestFloor(capsule != null ? capsule.radius : .35f, out string farRoom));
            var before = director.Snapshot;
            SuiteSelect.Invoke(director, new object[] { body });
            Assert.That(director.IsPanelOpen, Is.False, "From the " + farRoom + " the click opens nothing yet,");
            Assert.That(NpcRead<string>("headingToNpcId"), Is.EqualTo(pitcher), "it walks the player over to them.");
            int frames = 0;
            while (!director.IsPhasePanelOpen && frames < 2 * SuiteTripFrameBound) { frames++; yield return null; }
            Assert.That(director.IsPhasePanelOpen, Is.True, "On arrival the nomination's own panel opens on the pitches (" + frames + " frames).");
            Assert.That(director.TalkingToId, Is.Null, "A pitch opens no conversation,");
            Assert.That(director.CurrentAskWay, Is.EqualTo(EpisodeDirector.AskWay.None), "and no ask dialogue.");
            Assert.That(director.IsAskSetAside(pitchId), Is.True, "Their pitch is set aside: they will not walk up with it as well.");
            Assert.That(NpcRead<Dictionary<string, int>>("askPresentations").ContainsKey(pitchId), Is.False, "The player went to them: no presentation is counted.");
            var opened = director.Snapshot;
            Assert.That(opened.replyCards.Select(card => card.id), Is.EqualTo(comesPitchIds), "Going up answers nothing,");
            Assert.That(opened.windowActions, Is.EqualTo(before.windowActions), "costs no conversation,");
            Assert.That(opened.socialActions, Is.EqualTo(before.socialActions), "and no action.");
            Assert.That(opened.nominees, Is.Empty);
            LandThePanel();
            yield return null;

            var board = LastActive(EpisodeHud.OfferBoardName);
            Assert.That(board, Is.Not.Null, "The board,");
            var cards = OfferCards(board);
            Assert.That(cards.Select(card => card.name), Is.EqualTo(comesPitchIds.Select(id => EpisodeHud.OfferCardPrefix + opened.Find(ComesPitch(id).fromId).name)),
                "every card in its dealt order, the clicked pitcher's among them, unchanged.");
            AssertPitchBoardCaptions(board, "The board the suite opened");

            var card = cards.Single(row => row.name == EpisodeHud.OfferCardPrefix + opened.Find(pitcher).name);
            var answer = OfferButton(card, EpisodeHud.ReplyCaption(ReplyCards.Find(ReplyCards.Pitch, HoHPitches.HearKey).Label));
            ScrollIntoView(answer);
            AssertPressable(answer, answer.name);
            var pressed = director.Snapshot;
            answer.onClick.Invoke();
            var heard = director.Snapshot;
            yield return null; yield return null;
            AssertThePitchAnswer(pressed, heard, pitchId, HoHPitches.HearKey, "Hearing the suite's pitcher on the board");

            // Answered, they leave the suite; the others still wait.
            director.ClosePanels();
            yield return WaitFor(() => !director.SuitePitchersForDiagnostics.Contains(pitcher), 3f, "The answered pitcher is let go.");
            Assert.That(director.SuitePitchersForDiagnostics, Is.EquivalentTo(pitchers.Where(id => id != pitcher)), "The others still wait.");
        }

        // ------------------------------------------------------------------ 3. before the role moments: the one waiting first, on their own pitch

        [UnityTest, Timeout(300000)]
        public IEnumerator HoHSuite_PitchBeforeR7TheOneWaitingFirstOpensTheirOwn() => PinnedClock(PitchBeforeR7Held);

        private IEnumerator PitchBeforeR7Held() => WithTheHouseHeld(() => InstallComesToYouPitchHouse(false, false), PitchBeforeR7);

        private IEnumerator PitchBeforeR7()
        {
            Assert.That(SuiteSelect, Is.Not.Null);
            string first = comesPitchIds[0], pitcher = ComesPitch(first).fromId;
            Assert.That(EpisodeDirector.SuitePitcherIds(director.Snapshot), Is.EqualTo(new[] { pitcher }),
                "Before the board only the pitcher whose pitch waits first is listed, so only they go up.");
            yield return SuitePitchersArrive(new[] { pitcher });
            Assert.That(director.SuitePitchersForDiagnostics, Is.EquivalentTo(new[] { pitcher }));

            // Within reach, the click opens their pitch at once.
            var body = SuiteBody(pitcher);
            StandBeside(body);
            var before = director.Snapshot;
            byte[] bytes = File.ReadAllBytes(director.SavePath);
            SuiteSelect.Invoke(director, new object[] { body });
            // The press frame.
            Assert.That(director.IsPhasePanelOpen, Is.True, "The click opens the nomination's own panel,");
            Assert.That(director.TalkingToId, Is.Null, "not a conversation.");
            AssertEquivalent(before, director.Snapshot);
            CollectionAssert.AreEqual(bytes, File.ReadAllBytes(director.SavePath), "Hearing commits nothing.");
            yield return null;
            LandThePanel();
            yield return null;

            var state = director.Snapshot;
            Assert.That(LastActive(EpisodeHud.OfferBoardName), Is.Null, "No board before the role moments:");
            Assert.That(ActiveRect(EpisodeHud.StoryStepName), Is.Not.Null, "the one-at-a-time answer,");
            Assert.That(ShownText(), Does.Contain(ReplyCards.Title(state, ComesPitch(first))), "on the clicked pitcher's own pitch.");
            var pressed = director.Snapshot;
            ButtonWithCaption(HoHPitchCaption(HoHPitches.HearKey)).onClick.Invoke();
            AssertHoHPitchCommit(pressed, HoHPitches.HearKey);

            // Answered, they go; the pitch now waiting first is the next to go up.
            director.ClosePanels();
            string next = ComesPitch(comesPitchIds[1]).fromId;
            yield return WaitFor(() => director.SuitePitchersForDiagnostics.SequenceEqual(new[] { next }), 3f, "The next pitcher waits instead.");
            Assert.That(EpisodeDirector.SuitePitcherIds(director.Snapshot), Is.EqualTo(new[] { next }));
        }

        // ------------------------------------------------------------------ 4. the names said, everybody goes

        [UnityTest, Timeout(300000)]
        public IEnumerator HoHSuite_PitchersLeaveWhenTheNamesAreSaid() => PinnedClock(PitchersLeaveHeld);

        private IEnumerator PitchersLeaveHeld() => WithTheHouseHeld(() => InstallComesToYouPitchHouse(true, false), PitchersLeave);

        private IEnumerator PitchersLeave()
        {
            var pitchers = SuitePitcherOrder();
            yield return SuitePitchersArrive(pitchers);
            var state = director.Snapshot;
            var named = EpisodeEngine.NominationCandidates(state).Select(c => c.id).Where(id => !pitchers.Contains(id)).Take(2).ToArray();
            Assert.That(named, Has.Length.EqualTo(2), "Two houseguests who are not pitching to name.");
            var result = director.Submit(new EpisodeCommand
            {
                id = "hoh-suite-pitch-nominate", actorId = state.playerId, expectedPhase = state.phase,
                expectedRevision = state.revision, kind = EpisodeCommandKind.Nominate, targetId = named[0], secondTargetId = named[1],
            });
            Assert.That(result.accepted, Is.True, result.reason);
            Assert.That(EpisodeDirector.SuitePitcherIds(director.Snapshot), Is.Empty, "The names said, nobody pitches.");
            director.ClosePanels();
            yield return WaitFor(() => director.SuitePitchersForDiagnostics.Count == 0, 3f, "The suite lets every pitcher go.");
            Assert.That(pitchers.Any(director.SuitePitcherTripForDiagnostics), Is.False, "No trip to it.");
            yield return Frames(30);
            Assert.That(director.SuitePitchersForDiagnostics, Is.Empty, "Nobody is sent up again.");
        }

        // ------------------------------------------------------------------ 5. with the reply cards' asks off, nobody is placed

        [UnityTest]
        public IEnumerator HoHSuite_PitchersWaitOnlyWithTheReplyAsksOn() => PinnedClock(PitchersOffHeld);

        private IEnumerator PitchersOffHeld() => WithTheHouseHeld(() => InstallComesToYouPitchHouse(true, false), PitchersOff);

        private IEnumerator PitchersOff()
        {
            var pitchers = SuitePitcherOrder();
            director.ReplyAsksEnabled = false;
            yield return Frames(10);
            Assert.That(EpisodeDirector.SuitePitcherIds(director.Snapshot), Is.EqualTo(pitchers), "The season still lists them,");
            Assert.That(director.SuitePitchersForDiagnostics, Is.Empty, "but with the reply cards off nobody is placed,");
            Assert.That(pitchers.Any(director.SuitePitcherTripForDiagnostics), Is.False);
            Assert.That(pitchers.Any(director.SuitePitcherStandsThere), Is.False, "so no click hears a pitch.");

            director.ReplyAsksEnabled = true;
            director.AsksEnabled = false;
            yield return Frames(10);
            Assert.That(director.SuitePitchersForDiagnostics, Is.Empty, "nor with asks off.");
            Assert.That(pitchers.Any(director.SuitePitcherTripForDiagnostics), Is.False);
        }

        // ------------------------------------------------------------------ 6. a pitcher's own walk-up takes them from the suite, and gives them back

        [UnityTest, Timeout(300000)]
        public IEnumerator HoHSuite_PitchWalkUpTakesItsPitcherAndGivesThemBack() => PinnedClock(PitchWalkUpHeld);

        private IEnumerator PitchWalkUpHeld() => WithTheHouseHeld(() => InstallComesToYouPitchHouse(true), PitchWalkUp);

        private IEnumerator PitchWalkUp()
        {
            var pitchers = SuitePitcherOrder();
            var shown = new List<HouseNpcInvitationBubble>();
            yield return WaitForThePitchLine(comesPitchIds[0], shown);
            string walker = pitchers[0];
            Assert.That(director.CurrentAskerId, Is.EqualTo(walker), "The board's first walks up,");
            Assert.That(director.SuitePitcherTripForDiagnostics(walker), Is.False, "the walk-up has them, not the suite,");
            Assert.That(director.SuitePitchersForDiagnostics, Does.Contain(walker), "though their pitch still waits.");
            yield return WaitFor(() => pitchers.Skip(1).All(director.SuitePitcherTripForDiagnostics), 5f, "The others go up meanwhile.");

            // Let it wait: the visit goes, and the suite has them again.
            shown.Single().NotNowControl.onClick.Invoke();
            Assert.That(director.CurrentNpcInvitationRecordId, Is.Null, "The walker goes.");
            yield return WaitFor(() => director.SuitePitcherTripForDiagnostics(walker), 3f, "Back to the suite.");
            Assert.That(director.Snapshot.replyCards.Select(card => card.id), Is.EqualTo(comesPitchIds), "Nothing answered.");
        }

        // ------------------------------------------------------------------ 7. E on a pitcher there, as a click (stack O2's fixes)

        private static readonly MethodInfo SuiteInteract =
            typeof(EpisodeDirector).GetMethod("TalkOnInteract", BindingFlags.Instance | BindingFlags.NonPublic);

        /// <summary>
        /// The review's finding on HR-2: E within reach of a pitcher standing in or by the suite hears their pitch as a click
        /// does - the nomination's own panel on the pitches, never a conversation or a talk spot - and commits nothing.
        /// </summary>
        [UnityTest, Timeout(300000)]
        public IEnumerator HoHSuite_PitchEOnAPitcherThereOpensThePitches() => PinnedClock(PitchEKeyHeld);

        private IEnumerator PitchEKeyHeld() => WithTheHouseHeld(() => InstallComesToYouPitchHouse(true, false), PitchEKey);

        private IEnumerator PitchEKey()
        {
            Assert.That(SuiteInteract, Is.Not.Null);
            var pitchers = SuitePitcherOrder();
            yield return SuitePitchersArrive(pitchers);
            string pitchId = comesPitchIds[1], pitcher = pitchers[1];
            var body = SuiteBody(pitcher);
            StandBeside(body);
            var before = director.Snapshot;
            byte[] bytes = File.ReadAllBytes(director.SavePath);
            SuiteInteract.Invoke(director, new object[] { body });
            // The press frame.
            Assert.That(director.IsPhasePanelOpen, Is.True, "E opens the nomination's own panel,");
            Assert.That(director.TalkingToId, Is.Null, "not a conversation,");
            Assert.That(director.CurrentAskWay, Is.EqualTo(EpisodeDirector.AskWay.None), "nor an ask dialogue.");
            Assert.That(director.IsAskSetAside(pitchId), Is.True, "Their pitch is set aside, as a click sets it.");
            AssertEquivalent(before, director.Snapshot);
            CollectionAssert.AreEqual(bytes, File.ReadAllBytes(director.SavePath), "Hearing commits nothing.");
            yield return null;
            LandThePanel();
            yield return null;
            var board = LastActive(EpisodeHud.OfferBoardName);
            Assert.That(board, Is.Not.Null, "The board,");
            Assert.That(OfferCards(board), Has.Length.EqualTo(comesPitchIds.Length), "every pitch on it.");
            director.ClosePanels();
        }

        // ------------------------------------------------------------------ 8. the pitchers' spots: off the furniture and apart, by the picker's own checks

        private static readonly MethodInfo PitcherSpotPicker =
            typeof(EpisodeDirector).GetMethod("TryPickPitcherSpot", BindingFlags.Instance | BindingFlags.NonPublic);

        /// <summary>The pitcher spot picker's answer for this body, called as the trip calls it; the spot, or null for none.</summary>
        private Vector3? PickPitcherSpot(HouseNpc npc)
        {
            var args = new object[] { npc, null };
            return (bool)PitcherSpotPicker.Invoke(director, args) ? (Vector3?)(Vector3)args[1] : null;
        }

        /// <summary>
        /// The review's finding on HR-2: a pitcher's spot keeps off the furniture and clear of every other pitcher's chosen spot
        /// by the picker's own checks, not by chance. With the picker's one marker moved onto the suite's furniture, and then
        /// onto another pitcher's chosen spot with that pitcher's body stepped away from it, the first spot it tries - the
        /// marker's own - is refused each time, and every spot it gives is off the furniture and apart from every chosen spot.
        /// </summary>
        [UnityTest, Timeout(300000)]
        public IEnumerator HoHSuite_PitcherSpotsKeepOffTheFurnitureAndApart() => PinnedClock(PitcherSpotsHeld);

        private IEnumerator PitcherSpotsHeld() => WithTheHouseHeld(() => InstallComesToYouPitchHouse(true, false), PitcherSpots);

        private IEnumerator PitcherSpots()
        {
            Assert.That(PitcherSpotPicker, Is.Not.Null);
            var pitchers = SuitePitcherOrder();
            yield return SuitePitchersArrive(pitchers);
            // Somebody who is not pitching asks the picker, so their own spot is never the one kept clear of.
            var asker = SceneComponents<HouseNpc>().First(npc => npc.gameObject.activeInHierarchy && !pitchers.Contains(npc.Id));
            var capsule = asker.GetComponent<CapsuleCollider>();
            float radius = capsule != null ? capsule.radius : .35f, height = capsule != null ? capsule.height : 1.9f;
            float apart = 2f * radius + .3f;
            var chosen = pitchers.Select(id => director.SuitePitcherSpotForDiagnostics(id)).ToArray();
            Assert.That(chosen.All(spot => !float.IsNaN(spot.x)), Is.True, "Every pitcher has a chosen spot.");
            var marker = ActiveRoomMarkers().First(m => m.RoomName == EpisodeDirector.SuiteRoom);
            var home = marker.transform.position;
            // A pitcher whose chosen spot is on the suite's own floor, where the picker looks first.
            int inSuite = System.Array.FindIndex(chosen, spot => RoomAt(spot, radius) == EpisodeDirector.SuiteRoom);
            Assert.That(inSuite, Is.GreaterThanOrEqualTo(0), "The premise: a pitcher's chosen spot is in the suite.");
            var moved = SuiteBody(pitchers[inSuite]);
            var movedFrom = moved.transform.position;
            try
            {
                NpcWrite("suiteMarkers", new[] { marker });

                // On the furniture.
                var furniture = SuiteFurnitureFloor(home, radius, height);
                marker.transform.position = new Vector3(furniture.x, home.y, furniture.z);
                int found = 0;
                for (int pick = 0; pick < 30; pick++)
                {
                    var spot = PickPitcherSpot(asker);
                    if (spot == null) continue;
                    found++;
                    Assert.That(SuiteFeetInFurniture(spot.Value, radius, height), Is.False, "Off the furniture (pick " + pick + ").");
                    foreach (var other in chosen)
                        Assert.That(FlatDistance(spot.Value, other), Is.GreaterThanOrEqualTo(apart - .01f), "Apart from every chosen spot (pick " + pick + ").");
                }
                Assert.That(found, Is.GreaterThan(0), "With the marker on the furniture the picker still finds clear floor.");

                // On that pitcher's chosen spot, their body stepped away, so only the keep-apart check can refuse it.
                var agent = moved.GetComponent<NavMeshAgent>();
                var away = SuiteFarthestFloor(radius, out _);
                if (agent != null && agent.enabled) Assert.That(agent.Warp(away), Is.True);
                else moved.transform.position = away;
                Physics.SyncTransforms();
                marker.transform.position = new Vector3(chosen[inSuite].x, home.y, chosen[inSuite].z);
                found = 0;
                for (int pick = 0; pick < 30; pick++)
                {
                    var spot = PickPitcherSpot(asker);
                    if (spot == null) continue;
                    found++;
                    Assert.That(FlatDistance(spot.Value, chosen[inSuite]), Is.GreaterThanOrEqualTo(apart - .01f),
                        "Another pitcher's chosen spot is kept clear even with nobody standing on it (pick " + pick + ").");
                }
                Assert.That(found, Is.GreaterThan(0), "and the picker finds another spot.");
            }
            finally
            {
                marker.transform.position = home;
                NpcWrite("suiteMarkers", null);
                var agent = moved != null ? moved.GetComponent<NavMeshAgent>() : null;
                if (agent != null && agent.enabled) agent.Warp(movedFrom);
                Physics.SyncTransforms();
            }
        }
    }
}
