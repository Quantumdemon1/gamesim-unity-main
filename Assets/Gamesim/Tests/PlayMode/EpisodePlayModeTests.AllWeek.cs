using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Gamesim.Episode;
using Gamesim.House;
using Gamesim.Persistence;
using Gamesim.Presentation;
using Gamesim.Simulation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.TestTools;

namespace Gamesim.Tests.PlayMode
{
    /// <summary>
    /// D2 in the house (WAVE-D-NPC-PACTS-PLAN §4.4, §4.6): an act staged where it happened, seen by a player who
    /// walks in on it - one line, the act seen - and never from another room, under a panel or during a
    /// ceremony; a reload stages it again and says nothing twice; the house's Listen in hears an act the player
    /// saw; and the status line after the house's beats still names the step. The fixtures set the all-week
    /// rules' start week themselves, in InstallDiaryFixture's way, and ask the director for the stage in a batch
    /// run (<see cref="EpisodeDirector.ActsInBatchRuns"/>), the house's world built past free time. The house
    /// stages and watches on the real clock, so each test waits for the state the watch exposes, never for frames.
    /// The engine's half is AllWeekCadenceTests and WitnessNpcActTests.
    /// </summary>
    public sealed partial class EpisodePlayModeTests
    {
        /// <summary>
        /// A fresh season as the director starts one, the all-week rules on from week one, walked on the engine to
        /// the first state <paramref name="at"/> takes, shaped, saved to this test's slot and loaded as a player loads one.
        /// </summary>
        private IEnumerator InstallAllWeekFixture(System.Func<EpisodeState, bool> at, System.Action<EpisodeState> shape, string what)
        {
            EpisodeState fixture = null;
            for (uint seed = 1; seed <= 40 && fixture == null; seed++)
            {
                var start = ContentCatalog.Create(seed);
                ShippedRules.ApplyFresh(start);
                start.allWeekRulesStartWeek = 1;
                var engine = new EpisodeEngine(start);
                for (int guard = 0; guard < 400; guard++)
                {
                    var current = engine.Snapshot;
                    if (at(current)) { fixture = current; break; }
                    if (current.phase == EpisodePhase.Finished) break;
                    var result = engine.Apply(NextCommand(current));
                    Assert.That(result.accepted, Is.True, result.reason);
                }
            }
            Assert.That(fixture, Is.Not.Null, "No bounded legal fixture: " + what);
            shape?.Invoke(fixture);
            Assert.That(EpisodeValidation.TryValidate(fixture, out var reason), Is.True, reason);
            new EpisodeSaveStore(director.SavePath).Save(fixture);
            yield return ReloadEpisode();
            AssertEquivalent(fixture, director.Snapshot);
        }

        /// <summary>The window after the Head of Household, the names not yet said: the house paused, its people strolling.</summary>
        private static bool AtTheNominations(EpisodeState s) =>
            s.phase == EpisodePhase.Nomination && s.nominees.Count == 0 && s.pendingDiary == null
            && s.Find(s.playerId).status == ContestantStatus.Active && s.Active.Count(c => !c.isPlayer) >= 4;

        /// <summary>
        /// An act of the open window between the first two houseguests in the house, at the window's tick, staged
        /// in <paramref name="room"/>; the window's other acts cleared, so it is the house's to stage.
        /// </summary>
        private static NpcActState WriteAct(EpisodeState s, string kind, string room, bool sighted)
        {
            int window = EpisodeEngine.Window(s);
            s.npcSocial.acts.RemoveAll(a => a.window == window);
            var two = s.Active.Where(c => !c.isPlayer).Select(c => c.id).Take(2).ToList();
            var act = new NpcActState
            {
                id = s.week + "-" + window + "-90", kind = kind, actorId = two[0], partnerId = two[1], room = room,
                week = s.week, window = window, firedTick = EpisodeEngine.WindowTick(s, window), sighted = sighted,
            };
            s.npcSocial.acts.Add(act);
            return act;
        }

        /// <summary>The stage asked for in a batch run, the house's world built, the clock pinned and the house playing in the background.</summary>
        private void PlayTheActs()
        {
            Application.runInBackground = true;
            Time.captureDeltaTime = 1f / 30f;
            director.ActsInBatchRuns = true;
            director.BuildNpcWorldForDiagnostics();
            Assert.That(director.NpcAutonomyDiagnostic, Is.Null);
        }

        private static void StopPlayingTheActs() => Time.captureDeltaTime = 0f;

        private static Transform ActBody(string id) =>
            SceneComponents<HouseNpc>().FirstOrDefault(npc => npc.Id == id && npc.gameObject.activeInHierarchy)?.transform;

        /// <summary>
        /// The room a body standing at a point is in, as the house's watch reads it: the floor under it
        /// (<see cref="HouseRoomQuery.TryLocate"/>), null off every floor's safe interior.
        /// </summary>
        private static string RoomAt(Vector3 point, float radius)
        {
            Assert.That(HouseRoomQuery.TryCreate(UnityEngine.SceneManagement.SceneManager.GetSceneByName(EpisodeScene), out var rooms, out var reason), Is.True, reason);
            return rooms.TryLocate(point, radius, out var room) ? room : null;
        }

        /// <summary>The room a body is in, read with its own capsule, as the watch reads a houseguest.</summary>
        private static string RoomAt(Transform body)
        {
            var capsule = body.GetComponent<CapsuleCollider>();
            return RoomAt(body.position, capsule != null ? capsule.radius : .35f);
        }

        /// <summary>The house's active room markers.</summary>
        private static IEnumerable<HouseRoomMarker> ActiveRoomMarkers() =>
            SceneComponents<HouseRoomMarker>().Where(m => m.isActiveAndEnabled && !string.IsNullOrEmpty(m.RoomName));

        private static float Apart(Vector3 a, Vector3 b) { a.y = b.y = 0f; return Vector3.Distance(a, b); }

        /// <summary>Waits, on the real clock, until the house has staged the act and its two stand together in its room.</summary>
        private IEnumerator WaitForTheActStaged(NpcActState act, float seconds = 40f)
        {
            float deadline = Time.realtimeSinceStartup + seconds;
            while (Time.realtimeSinceStartup < deadline && !Together(act)) yield return null;
            Assert.That(director.StagedActs, Has.Member(act.id), "The house staged the act.");
            Assert.That(Together(act), Is.True, "Its two stand together in the " + act.room + ".");
        }

        private bool Together(NpcActState act)
        {
            if (!director.StagedActs.Contains(act.id)) return false;
            var one = ActBody(act.actorId); var two = ActBody(act.partnerId);
            return one != null && two != null && RoomAt(one) == act.room && RoomAt(two) == act.room
                && Apart(one.position, two.position) <= EpisodeDirector.WalkInPairMetres;
        }

        /// <summary>Puts the player a couple of metres from the act's first, in its room.</summary>
        private void WarpBeside(NpcActState act)
        {
            var one = ActBody(act.actorId);
            Assert.That(one, Is.Not.Null);
            for (int direction = 0; direction < 16; direction++)
            {
                float angle = direction * Mathf.PI / 8f, reach = direction < 8 ? 2f : 3.5f;
                var candidate = one.position + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * reach;
                if (!NavMesh.SamplePosition(candidate, out var hit, .5f, player.Agent.areaMask) || RoomAt(hit.position, player.Agent.radius) != act.room) continue;
                if (!player.Agent.Warp(hit.position)) continue;
                player.Agent.ResetPath();
                Physics.SyncTransforms();
                return;
            }
            Assert.Fail("No floor beside " + act.actorId + " in the " + act.room + ".");
        }

        /// <summary>
        /// Puts the player in a room, on its floor near its marker: where an act staged there would stand, without
        /// needing the act's two to be there.
        /// </summary>
        private void WarpInto(string room)
        {
            foreach (var marker in ActiveRoomMarkers().Where(m => m.RoomName == room))
                for (int direction = 0; direction < 17; direction++)
                {
                    float angle = direction * Mathf.PI / 8f, reach = direction == 0 ? 0f : direction <= 8 ? 1f : 2f;
                    var candidate = marker.transform.position + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * reach;
                    if (!NavMesh.SamplePosition(candidate, out var hit, 1.5f, player.Agent.areaMask) || RoomAt(hit.position, player.Agent.radius) != room) continue;
                    if (!player.Agent.Warp(hit.position)) continue;
                    player.Agent.ResetPath();
                    Physics.SyncTransforms();
                    return;
                }
            Assert.Fail("No floor in the " + room + ".");
        }

        /// <summary>Puts the player in a room that is not the act's.</summary>
        private void WarpElsewhere(NpcActState act)
        {
            foreach (var marker in ActiveRoomMarkers().Where(m => m.RoomName != act.room && m.RoomName != "HoH" && m.RoomName != "Private"))
            {
                if (!NavMesh.SamplePosition(marker.transform.position, out var hit, 1.5f, player.Agent.areaMask)) continue;
                string there = RoomAt(hit.position, player.Agent.radius);
                if (there == null || there == act.room) continue;
                if (!player.Agent.Warp(hit.position)) continue;
                player.Agent.ResetPath();
                Physics.SyncTransforms();
                return;
            }
            Assert.Fail("No other room to stand in.");
        }

        private NpcActState ActNow(string id) => director.Snapshot.npcSocial.acts.FirstOrDefault(a => a.id == id);

        /// <summary>Waits, on the real clock, until the act is seen or the time is up.</summary>
        private IEnumerator WaitUntilSeen(NpcActState act, float seconds)
        {
            float deadline = Time.realtimeSinceStartup + seconds;
            while (Time.realtimeSinceStartup < deadline && ActNow(act.id)?.sighted != true) yield return null;
        }

        /// <summary>Holds the player where they are for a stretch of the real clock - longer than the watch's hold - doing <paramref name="each"/> every frame.</summary>
        private static IEnumerator Linger(float seconds, System.Action each = null)
        {
            float deadline = Time.realtimeSinceStartup + seconds;
            while (Time.realtimeSinceStartup < deadline) { each?.Invoke(); yield return null; }
        }

        private int Sightings(EpisodeState s) => s.events.Count(e => e.kind == WaveDEventKinds.Sighting || e.kind == WaveDEventKinds.Overheard);

        [UnityTest, Timeout(300000)]
        public IEnumerator AllWeek_OneWitnessGivesOneLineAndTheActIsSeen()
        {
            NpcActState act = null;
            yield return InstallAllWeekFixture(AtTheNominations, s => act = WriteAct(s, NpcActKinds.Talk, "Kitchen", false), "the nominations' window");
            PlayTheActs();
            try
            {
                yield return WaitForTheActStaged(act);
                var before = director.Snapshot;
                WarpBeside(act);
                yield return WaitUntilSeen(act, 20f);
                var after = director.Snapshot;
                var seen = ActNow(act.id);
                Assert.That(seen.sighted, Is.True, "The player walked in on them and saw it.");
                Assert.That(seen.overheard, Is.False, "A word, not a fight.");
                var said = after.events.Where(e => e.sequence >= before.nextSequence && (e.kind == WaveDEventKinds.Sighting || e.kind == WaveDEventKinds.Overheard)).ToList();
                Assert.That(said, Has.Count.EqualTo(1), "One line.");
                Assert.That(said[0].text, Is.EqualTo(EpisodeEngine.SightingLine(after, seen)));
                Assert.That(said[0].text, Does.EndWith(" talking in the kitchen."));
                Assert.That(said[0].audienceIds, Is.EqualTo(new[] { after.playerId }), "To the player alone.");
                yield return Linger(3f);
                Assert.That(Sightings(director.Snapshot), Is.EqualTo(Sightings(after)), "and never again.");
                Assert.That(director.StagedActs, Has.No.Member(act.id), "A seen act is let go.");
            }
            finally { StopPlayingTheActs(); }
        }

        [UnityTest, Timeout(300000)]
        public IEnumerator AllWeek_NoSightingFromAnotherRoomUnderAPanelOrDuringACeremony()
        {
            NpcActState act = null;
            yield return InstallAllWeekFixture(AtTheNominations, s => act = WriteAct(s, NpcActKinds.Pact, "Bedroom", false), "the nominations' window");
            PlayTheActs();
            try
            {
                yield return WaitForTheActStaged(act);
                int lines = Sightings(director.Snapshot);

                WarpElsewhere(act);
                yield return Linger(4f);
                Assert.That(ActNow(act.id).sighted, Is.False, "Not from another room.");

                WarpBeside(act);
                director.ShowNotebookSection(EpisodeDirector.NotebookSection.Alliances);
                Assert.That(director.IsPanelOpen, Is.True);
                yield return Linger(4f);
                Assert.That(ActNow(act.id).sighted, Is.False, "Not under a panel.");
                director.ClosePanels();
                yield return null;

                WarpBeside(act);
                yield return Linger(4f, CeremonyOverlays.Showing);
                Assert.That(ActNow(act.id).sighted, Is.False, "Not while a ceremony is on screen.");
                Assert.That(Sightings(director.Snapshot), Is.EqualTo(lines), "No line.");

                // With nothing in the way the same place sees it: the guards, not the geometry, held it.
                WarpBeside(act);
                yield return WaitUntilSeen(act, 20f);
                Assert.That(ActNow(act.id).sighted, Is.True, "Seen once nothing is in the way.");
                Assert.That(Sightings(director.Snapshot), Is.EqualTo(lines + 1));
            }
            finally { StopPlayingTheActs(); }
        }

        [UnityTest, Timeout(300000)]
        public IEnumerator AllWeek_AReloadStagesTheActAgainAndSaysNothingTwice()
        {
            NpcActState act = null;
            yield return InstallAllWeekFixture(AtTheNominations, s => act = WriteAct(s, NpcActKinds.Confront, "Living", false), "the nominations' window");
            PlayTheActs();
            try
            {
                yield return WaitForTheActStaged(act);
                director.SaveNow();
                director.LoadNow();
                yield return null;
                director.BuildNpcWorldForDiagnostics();
                yield return WaitForTheActStaged(act);
                Assert.That(ActNow(act.id).sighted, Is.False, "Staged again after the load, unseen.");

                WarpBeside(act);
                yield return WaitUntilSeen(act, 20f);
                var seen = ActNow(act.id);
                Assert.That(seen.sighted && seen.overheard, Is.True, "A fight is seen and heard.");
                int lines = Sightings(director.Snapshot);
                Assert.That(director.Snapshot.events.Last(e => e.kind == WaveDEventKinds.Overheard).text, Does.EndWith(" in the living room."));

                director.SaveNow();
                director.LoadNow();
                yield return null;
                director.BuildNpcWorldForDiagnostics();
                // The load puts everyone back at their starting places and a seen act is never staged again, so the
                // player stands in its room by the room's marker, not beside a body that is no longer there for it.
                WarpInto(act.room);
                yield return Linger(4f);
                Assert.That(director.StagedActs, Has.No.Member(act.id), "A seen act is not staged again.");
                Assert.That(Sightings(director.Snapshot), Is.EqualTo(lines), "and nothing is said twice.");
            }
            finally { StopPlayingTheActs(); }
        }

        /// <summary>
        /// The house's Listen in, found by its caption on the Nearby card, at the final three's free time - two
        /// houseguests left, so the pair it overhears is theirs - hears the act the player saw them at: today's line
        /// and the act's clause after it, once, with only D4's sentence after the clause (seed 1's two share a pact).
        /// </summary>
        [UnityTest, Timeout(300000)]
        public IEnumerator AllWeek_ListenInHearsAnActThePlayerSaw()
        {
            NpcActState act = null;
            yield return InstallAllWeekFixture(
                s => s.phase == EpisodePhase.Social && s.evictionResolved && s.Active.Count() == 3 && s.pendingDiary == null
                     && s.Find(s.playerId).status == ContestantStatus.Active
                     && EpisodeEngine.SocialActionsSpent(s) < EpisodeEngine.SocialActionBudget(s),
                s =>
                {
                    act = WriteAct(s, NpcActKinds.Promise, "Bedroom", true);
                    // The season's stream where the listen-in goes unnoticed: found on the engine first.
                    for (uint attempt = 1; attempt < 400; attempt++)
                    {
                        var probe = s.Clone();
                        probe.randomState = attempt * 2654435761u;
                        var heard = new EpisodeEngine(probe).Apply(new EpisodeCommand
                        {
                            id = "probe-" + attempt, actorId = probe.playerId, kind = EpisodeCommandKind.Eavesdrop,
                            expectedRevision = probe.revision, expectedPhase = probe.phase,
                        });
                        if (!heard.accepted || !heard.state.events.Any(e => e.sequence >= probe.nextSequence && e.kind == "eavesdrop"
                                && e.text.StartsWith("You overheard ", System.StringComparison.Ordinal))) continue;
                        s.randomState = probe.randomState;
                        return;
                    }
                    Assert.Fail("No listen-in went unnoticed.");
                },
                "the final three's free time");
            HoldTheHouseForTheFixture();
            yield return null;
            var state = director.Snapshot;
            string clause = EpisodeEngine.ActClause(state, act);
            Assert.That(clause, Does.Contain(" their word."));
            SceneComponents<HouseConversationCaption>().First().Show(state.Find(act.actorId).name, state.Find(act.partnerId).name, "strategy", 1f);
            yield return null; yield return null;
            Assert.That(director.CanListenIn, Is.True);
            var before = director.Snapshot;
            ButtonWithCaption(EpisodeHud.ListenInCaption).onClick.Invoke();
            var after = director.Snapshot;
            Assert.That(after.revision, Is.EqualTo(before.revision + 1), "Listening in commits the house's Eavesdrop.");
            var line = after.events.Last(e => e.kind == "eavesdrop").text;
            Assert.That(line, Does.StartWith("You overheard "));
            // The builder's clause order (EpisodeEngine.EavesdropLine): today's line, the act's clause, then D4's
            // sentence where the listen-in made the player a knower of the two's pact - as in this final three,
            // whose two share one - and nothing else.
            int at = line.IndexOf(clause, System.StringComparison.Ordinal);
            Assert.That(at, Is.GreaterThan(0), "The act's clause, after today's line: " + line);
            Assert.That(line.IndexOf(clause, at + 1, System.StringComparison.Ordinal), Is.EqualTo(-1), "Said once: " + line);
            var pact = Knowledge.PactOfPair(before, act.actorId, act.partnerId, before.playerId);
            string sentence = pact == null ? "" : AllianceLeaks.ListenInSentence(after, pact);
            Assert.That(line.Substring(at + clause.Length), Is.EqualTo("").Or.EqualTo(sentence), "Nothing after the clause but D4's sentence: " + line);
            Assert.That(ActNow(act.id).overheard, Is.True, "Heard once.");
        }

        /// <summary>
        /// The status line after the Head of Household is crowned names the step - the phase's own line - and not a
        /// beat the house said to the player as the window opened (D2's decision 10). The house held, so nothing
        /// else commits between the fixture and the step.
        /// </summary>
        [UnityTest, Timeout(300000)]
        public IEnumerator AllWeek_TheStatusLineAfterTheCrowningNamesTheStep()
        {
            yield return InstallAllWeekFixture(
                s =>
                {
                    if (s.phase != EpisodePhase.HoH || !s.competitionResolved || s.pendingDiary != null) return false;
                    // A crowning after which a beat says something to the player.
                    var step = new EpisodeEngine(s).Apply(NextCommand(s));
                    return step.accepted && step.beatsFromSequence > 0 && step.state.events.Any(e => e.sequence >= step.beatsFromSequence
                        && (e.audienceIds.Count == 0 || e.audienceIds.Contains(s.playerId)));
                },
                null, "a crowning the house speaks after");
            HoldTheHouseForTheFixture();
            yield return null;
            var result = director.Submit(NextCommand(director.Snapshot));
            Assert.That(result.accepted, Is.True, result.reason);
            var after = result.state;
            Assert.That(after.phase, Is.EqualTo(EpisodePhase.Nomination));
            var beat = after.events.Last(e => e.audienceIds.Count == 0 || e.audienceIds.Contains(after.playerId));
            Assert.That(beat.sequence, Is.GreaterThanOrEqualTo(result.beatsFromSequence), "Precondition: the last line the player may see is a beat's.");
            var step = after.events.Last(e => (e.audienceIds.Count == 0 || e.audienceIds.Contains(after.playerId)) && e.sequence < result.beatsFromSequence);
            Assert.That(director.StatusMessage, Does.StartWith(EpisodeDirector.StatusLine(after, step)), "The step's line.");
            Assert.That(director.StatusMessage, Does.Not.Contain(EpisodeDirector.StatusLine(after, beat)), "Not the beat's.");
        }
    }
}
