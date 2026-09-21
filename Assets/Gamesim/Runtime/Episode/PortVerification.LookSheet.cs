using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Gamesim.House;
using Gamesim.Presentation;
using Gamesim.Simulation;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Gamesim.Episode
{
    /// <summary>
    /// The look sheet (VISUAL-TARGET.md, phase V0): twelve captures of the built player, one per
    /// mockup in <c>ArtSource/reference/mockups/</c>, written as <c>after-NN.png</c> beside a
    /// <c>look-sheet.json</c> that says which moments were reached and which were the nearest real
    /// frame. Run with <c>--gamesim-verify --gamesim-look-sheet --gamesim-save-root &lt;dir&gt;</c>
    /// in a window (<c>Tools/build-and-verify.sh --look-sheet</c>).
    ///
    /// <para>It is a separate mode, not a bolt-on to the recorded walk: it commits its own decisions
    /// (skipping the opening, a ballot in the diary, one small talk) so it must never share the
    /// walk's revision assertions. Every required moment and resolution/text-size capture must
    /// be reached; failures remain in the report. Capturing a frame establishes route coverage,
    /// while visual acceptance still requires inspection of the actual images.</para>
    /// </summary>
    public sealed partial class PortVerification
    {
        private bool lookSheet;

        [Serializable]
        private sealed class LookShot
        {
            public int index;
            public string mockup, label, path, reason, phase;
            public int week;
            public bool reached;
            public double secondsSinceStart;
        }

        [Serializable]
        private sealed class LookSheetReport
        {
            public string status, startedUtc, finishedUtc, resolution;
            public string visualStatus = "Pending inspection of captured frames";
            public int houseSize;
            public uint seed;
            public List<LookShot> shots = new List<LookShot>();
            public List<LayoutCapture> layouts = new List<LayoutCapture>();
            public List<string> errors = new List<string>();
        }

        [Serializable]
        private sealed class LayoutCapture
        {
            public int shot, width, height;
            public bool largeText, compactHud;
            public string path;
        }

        private LookSheetReport lookReport;
        private double lookStarted;

        private IEnumerator RunLookSheet()
        {
            lookStarted = Time.realtimeSinceStartupAsDouble;
            lookReport = new LookSheetReport { status = "Running", startedUtc = DateTime.UtcNow.ToString("O") };
            if (Application.isBatchMode || SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null)
            {
                lookReport.errors.Add("The look sheet needs a window; -batchmode captures are black.");
                yield break;
            }
            Screen.SetResolution(1920, 1080, FullScreenMode.Windowed);
            for (int i = 0; i < 15; i++) yield return null;
            lookReport.resolution = Screen.width + "x" + Screen.height;

            seasonDirector = FindAnyObjectByType<EpisodeDirector>();
            seasonPlayer = seasonDirector.gameObject.scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<HousePlayerController>(true)).First();
            seasonReport = seasonReport ?? new SeasonReport { artifactId = Guid.NewGuid().ToString("N") };
            yield return SkipOpening();

            // The whole Regular roster, so the mockups' eight faces are all in the house.
            seasonDirector.StartSeason(new SeasonBuilder.Choice { Roster = CastTemplates.Roster.Regular, HouseSize = 12 });
            for (int i = 0; i < 30; i++) yield return null;
            yield return SkipOpening();
            lookReport.houseSize = seasonDirector.Snapshot.contestants.Count;
            lookReport.seed = seasonDirector.Snapshot.seed;

            yield return CloseEverything();
            var exploration = new LookShot { index = 0, label = "exploration HUD", reached = true };
            yield return Guard(CaptureLayoutMatrix(exploration), exploration);
            if (!exploration.reached) lookReport.errors.Add("Exploration layout capture failed: " + exploration.reason);

            yield return Shot(2, "cast screen", CastScreen);
            yield return Shot(1, "living room, a houseguest selected, the action panel", LivingRoom);
            yield return Shot(3, "house overview", Overview);
            yield return Shot(7, "relationship graph", RelationshipGraph);
            yield return Shot(11, "diary room", DiaryRoom);
            yield return Shot(12, "kitchen conversation", KitchenConversation);
            yield return Shot(6, "night conversation", NightConversation);
            yield return Shot(4, "house-event choice", HouseEventChoice);
            yield return Shot(5, "competition mid-play", CompetitionMidPlay);
            yield return Shot(9, "nomination discussion", NominationDiscussion);
            yield return Shot(10, "key ceremony", KeyCeremonyShot);
            yield return Shot(8, "eviction ballot", EvictionBallot);
        }

        private void FinishLookSheet()
        {
            if (lookReport == null) return;
            int written = lookReport.shots.Count(s => s.path != null && File.Exists(s.path) && new FileInfo(s.path).Length > 0);
            lookReport.status = written == 12 && lookReport.shots.All(s => s.reached)
                && lookReport.layouts.Count == 31 && lookReport.errors.Count == 0 ? "Passed" : "Failed";
            lookReport.finishedUtc = DateTime.UtcNow.ToString("O");
            File.WriteAllText(Path.Combine(outputDirectory, "look-sheet.json"), JsonUtility.ToJson(lookReport, true));
            Debug.Log("Gamesim look sheet " + lookReport.status + ": " + written + " of 12 captures, "
                      + lookReport.shots.Count(s => s.reached) + " reached; " + Path.Combine(outputDirectory, "look-sheet.json"));
            Application.Quit(lookReport.status == "Passed" ? 0 : 3);
        }

        // ---------------------------------------------------------------- the frame

        /// <summary>Captures each route and records its failure without abandoning the remaining evidence.</summary>
        private IEnumerator Shot(int index, string label, Func<LookShot, IEnumerator> route)
        {
            var shot = new LookShot { index = index, mockup = "mockup-" + index.ToString("00"), label = label, reached = true };
            yield return Guard(route(shot), shot);
            // The capture and the tidy-up are guarded too: a moment that cannot be reached must still
            // leave a picture and a reason behind it, and the sheet must still reach its report.
            yield return Guard(CaptureLook(shot), shot);
            if (shot.reached && (index == 1 || index == 2 || index == 7 || index == 11))
                yield return Guard(CaptureLayoutMatrix(shot), shot);
            var state = seasonDirector.Snapshot;
            shot.phase = state.phase.ToString();
            shot.week = state.week;
            shot.secondsSinceStart = Time.realtimeSinceStartupAsDouble - lookStarted;
            lookReport.shots.Add(shot);
            yield return Guard(CloseEverything(), shot);
        }

        /// <summary>
        /// Runs a route and turns whatever it throws into that moment's reason.
        ///
        /// <para>The nested routines have to be driven here rather than handed to Unity. A coroutine
        /// that yields another coroutine hands it to Unity's runner, which iterates it outside this
        /// try - so the first thing a route threw from inside a nested wait escaped the guard, killed
        /// the whole sheet and left the player sitting on a window with no report. Flattening the
        /// stack here means every step of every nested routine is a MoveNext this catch can see.</para>
        /// </summary>
        private static IEnumerator Guard(IEnumerator inner, LookShot shot)
        {
            var stack = new Stack<IEnumerator>();
            stack.Push(inner);
            while (stack.Count > 0)
            {
                var top = stack.Peek();
                bool moved;
                try { moved = top.MoveNext(); }
                catch (Exception error) { shot.reached = false; shot.reason = Append(shot.reason, error.Message); yield break; }
                if (!moved) { stack.Pop(); continue; }
                // A nested routine (a wait, a walk, a click) is driven here; anything else - a null,
                // a YieldInstruction - is Unity's to wait on.
                if (top.Current is IEnumerator nested) { stack.Push(nested); continue; }
                yield return top.Current;
            }
        }

        private static string Append(string existing, string more) =>
            string.IsNullOrEmpty(existing) ? more : existing + " " + more;

        private IEnumerator CaptureLook(LookShot shot)
        {
            // No picture is taken over the title card. The opening plays itself whenever a season
            // starts and the cast screen starts one, so the one place that can be sure of this is
            // the one place that takes the pictures.
            yield return SkipOpening();
            Canvas.ForceUpdateCanvases();
            for (int frame = 0; frame < 5; frame++) yield return null;
            string path = Path.Combine(outputDirectory, "after-" + shot.index.ToString("00") + ".png");
            if (File.Exists(path)) File.Delete(path);
            ScreenCapture.CaptureScreenshot(path);
            double deadline = Time.realtimeSinceStartupAsDouble + 5;
            while ((!File.Exists(path) || new FileInfo(path).Length == 0) && Time.realtimeSinceStartupAsDouble < deadline) yield return null;
            yield return null; yield return null;
            if (!File.Exists(path) || new FileInfo(path).Length == 0)
            {
                lookReport.errors.Add("Capture " + shot.index + " was not written.");
                shot.reason = Append(shot.reason, "capture not written");
            }
            else shot.path = path;
        }

        // Render the existing interaction at each supported viewport/text size. These are visual
        // evidence, not a claim that having written a PNG proves absence of overlap or clipping.
        private IEnumerator CaptureLayoutMatrix(LookShot shot)
        {
            bool originalLarge = seasonDirector.LargeText, originalCompact = seasonDirector.CompactHud;
            int originalWidth = Screen.width, originalHeight = Screen.height;
            var originalMode = Screen.fullScreenMode;
            try
            {
                foreach (var size in new[] { new Vector2Int(1280,720), new Vector2Int(1600,900), new Vector2Int(1920,1080) })
                {
                    Screen.SetResolution(size.x,size.y,FullScreenMode.Windowed);
                    for (int frame=0;frame<15;frame++) yield return null;
                    foreach (bool large in new[] { false,true })
                    {
                        seasonDirector.SetLargeText(large);
                        seasonDirector.SetCompactHud(false);
                        yield return CaptureLayout(shot,large,false,size.x,size.y);
                    }
                }
                if (shot.index == 0)
                {
                    Screen.SetResolution(1280,720,FullScreenMode.Windowed);
                    for (int frame=0;frame<15;frame++) yield return null;
                    seasonDirector.SetLargeText(true);
                    seasonDirector.SetCompactHud(true);
                    yield return CaptureLayout(shot,true,true,1280,720);
                }
            }
            finally
            {
                seasonDirector.SetLargeText(originalLarge);
                seasonDirector.SetCompactHud(originalCompact);
                Screen.SetResolution(originalWidth,originalHeight,originalMode);
            }
            for (int frame=0;frame<15;frame++) yield return null;
        }

        private IEnumerator CaptureLayout(LookShot shot,bool large,bool compact,int requestedWidth,int requestedHeight)
        {
            Canvas.ForceUpdateCanvases();
            for (int frame=0;frame<5;frame++) yield return null;
            string filename="layout-"+shot.index.ToString("00")+"-"+Screen.width+"x"+Screen.height
                +(large ? "-large" : "-standard")+(compact ? "-compact" : "")+".png";
            string path=Path.Combine(outputDirectory,filename);
            if(File.Exists(path))File.Delete(path);
            ScreenCapture.CaptureScreenshot(path);
            double deadline=Time.realtimeSinceStartupAsDouble+5;
            while((!File.Exists(path)||new FileInfo(path).Length==0)&&Time.realtimeSinceStartupAsDouble<deadline)yield return null;
            if(!File.Exists(path)||new FileInfo(path).Length==0)
                lookReport.errors.Add("Layout capture was not written: "+filename);
            if(Screen.width!=requestedWidth||Screen.height!=requestedHeight)
                lookReport.errors.Add("Requested layout "+requestedWidth+"x"+requestedHeight+" rendered at "+Screen.width+"x"+Screen.height+".");
            lookReport.layouts.Add(new LayoutCapture { shot=shot.index,width=Screen.width,height=Screen.height,
                largeText=large,compactHud=compact,path=path });
            yield return null;
        }

        /// <summary>Back to a quiet house between moments: every panel closed, the cast free to move.</summary>
        private IEnumerator CloseEverything()
        {
            var cast = FindAnyObjectByType<CastSelect>();
            if (cast != null && cast.IsShowing) yield return ClickAny(CastSelect.CancelCaption);
            var recap = FindAnyObjectByType<WeeklyRecapScreen>();
            if (recap != null && recap.IsOpen) yield return ClickAny(WeeklyRecapScreen.ContinueCaption);
            // The front door too. The cast screen is reached through the main menu, and a menu left
            // showing sat over every capture that followed it - eleven pictures of the house behind
            // a title card, which is exactly the thing the sheet exists to notice.
            var menu = FindAnyObjectByType<MainMenu>();
            if (menu != null && menu.IsShowing && seasonDirector.SeasonInProgress) seasonDirector.CloseMainMenu();
            seasonDirector.ClosePanels();
            yield return null; yield return null;
            var stillShowing = FindAnyObjectByType<MainMenu>();
            if (stillShowing != null && stillShowing.IsShowing) throw new InvalidOperationException("The main menu would not close over the house.");
        }

        private IEnumerator SkipOpening()
        {
            var opening = FindAnyObjectByType<OpeningSequence>();
            if (opening != null && opening.IsPlaying) opening.Skip();
            var tutorial = FindAnyObjectByType<HouseTutorial>();
            if (tutorial != null && tutorial.IsShowing) tutorial.Skip();
            double deadline = Time.realtimeSinceStartupAsDouble + 10;
            while (opening != null && opening.IsPlaying && Time.realtimeSinceStartupAsDouble < deadline) yield return null;
            yield return null; yield return null;
        }

        /// <summary>A click on any active button under any root, by caption; the walk's helper only searches under the director.</summary>
        private IEnumerator ClickAny(string caption)
        {
            var button = SceneManager.GetActiveScene().GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<UnityEngine.UI.Button>(true))
                .FirstOrDefault(b => b.IsActive() && b.IsInteractable()
                    && b.GetComponentsInChildren<TMPro.TMP_Text>(true).Any(t => t.text == caption));
            if (button == null) throw new InvalidOperationException("No button '" + caption + "' on screen.");
            button.onClick.Invoke();
            yield return null; yield return null;
        }

        private static string NearestRoom(Vector3 at)
        {
            HouseRoomMarker best = null; float bestDistance = float.MaxValue;
            foreach (var marker in FindObjectsByType<HouseRoomMarker>(FindObjectsSortMode.None))
            {
                float d = Vector3.Distance(marker.transform.position, at);
                if (d < bestDistance) { bestDistance = d; best = marker; }
            }
            return best == null ? null : best.RoomName;
        }

        private HouseNpc[] ActiveNpcs()
        {
            var state = seasonDirector.Snapshot;
            return seasonDirector.gameObject.scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<HouseNpc>(true))
                .Where(npc => npc.gameObject.activeInHierarchy && state.Find(npc.Id) != null && state.Find(npc.Id).status == ContestantStatus.Active)
                .ToArray();
        }

        private IEnumerator ApproachAndOpen(HouseNpc npc, LookShot shot)
        {
            if (!FindSeasonNpcApproach(npc, out var approach)) throw new InvalidOperationException("No approach to " + npc.name + ".");
            if (!seasonPlayer.TryMoveTo(approach)) throw new InvalidOperationException("The walk to " + npc.name + " was refused.");
            yield return WaitSeasonWalk("look " + npc.name, approach, false);
            if (!seasonDirector.TryOpenNpc(npc.Id)) throw new InvalidOperationException(npc.name + " did not open for conversation.");
            yield return null; yield return null;
            yield return WaitForShot();
        }

        /// <summary>Lets a shot land - the two-shot, the chair, the overview - before the capture.</summary>
        private IEnumerator WaitForShot()
        {
            var rig = FindAnyObjectByType<HouseCameraRig>();
            if (seasonDirector.IsDiaryOpen)
            {
                yield return WaitUntil(() => seasonDirector.IsDiarySettled, 25);
                if (!seasonDirector.IsDiarySettled)
                    throw new TimeoutException("The diary did not reach its seated decision state before capture.");
            }
            if (rig == null || !rig.HasShot) yield break;
            bool Settled()
            {
                if (!rig.HasShot || !rig.HasArrived(.1f) || rig.IsTravelling
                    || Mathf.Abs(rig.DepthOfFieldWeight-rig.DesiredDepthOfFieldWeight)>.01f) return false;
                var diary = seasonPlayer.GetComponent<DiarySeatPose>();
                if (diary == null || !diary.Active) return true;
                var seat = seasonPlayer.GetComponent<HouseSeatPresentation>();
                var build = seasonPlayer.GetComponentInChildren<CharacterBodyBuildState>();
                return seat != null && seat.Settled && (build == null || build.Ready);
            }
            yield return WaitUntil(Settled, 15);
            if (!Settled()) throw new TimeoutException("The camera, depth of field or diary seating did not settle before capture.");
            yield return null;
        }

        private IEnumerator WaitUntil(Func<bool> condition, double seconds)
        {
            double deadline = Time.realtimeSinceStartupAsDouble + seconds;
            while (!condition() && Time.realtimeSinceStartupAsDouble < deadline) yield return null;
        }

        // ---------------------------------------------------------------- the twelve moments

        private IEnumerator CastScreen(LookShot shot)
        {
            seasonDirector.OpenMainMenu();
            yield return null; yield return null;
            yield return ClickAny(MainMenu.NewSeasonCaption);
            yield return WaitUntil(() => { var c = FindAnyObjectByType<CastSelect>(); return c != null && c.IsShowing; }, 5);
            var cast = FindAnyObjectByType<CastSelect>();
            if (cast == null || !cast.IsShowing) throw new InvalidOperationException("The cast screen did not open.");
        }

        private IEnumerator LivingRoom(LookShot shot)
        {
            var npc = ActiveNpcs().OrderBy(n => NearestRoom(n.transform.position) == "Living" ? 0 : 1)
                .ThenBy(n => Vector3.Distance(n.transform.position, new Vector3(-5.5f, 0f, -4f))).FirstOrDefault();
            if (npc == null) throw new InvalidOperationException("No active houseguest to select.");
            seasonDirector.FollowHouseguest(npc.Id);
            yield return ApproachAndOpen(npc, shot);
            shot.reason = "Conversation controls use the activity layout; relationship details remain in the notebook. "
                + "Compare the visible pair, house and cast clearance with the reference.";
        }

        private IEnumerator Overview(LookShot shot)
        {
            if (!seasonDirector.ShowOverview()) throw new InvalidOperationException("The overview did not open.");
            var rig = FindAnyObjectByType<HouseCameraRig>();
            yield return WaitUntil(() => rig.HasArrived() && rig.LensOrthographic >= 0.999f, 4);
            yield return null;
            shot.reason = "Inspect the authored cutaway, room labels and unobstructed house area around the HUD.";
        }

        private IEnumerator RelationshipGraph(LookShot shot)
        {
            seasonDirector.OpenJournal();
            yield return null;
            seasonDirector.ShowNotebookSection(EpisodeDirector.NotebookSection.Network);
            yield return null; yield return null;
        }

        private IEnumerator DiaryRoom(LookShot shot)
        {
            if (!seasonPlayer.TryMoveTo(seasonDirector.DiaryPosition)) throw new InvalidOperationException("The walk to the diary room was refused.");
            yield return WaitSeasonWalk("look diary", seasonDirector.DiaryPosition, false);
            if (!seasonDirector.TryOpenDiary()) throw new InvalidOperationException("The diary room did not open.");
            yield return null; yield return null;
            yield return WaitForShot();
        }

        private IEnumerator KitchenConversation(LookShot shot)
        {
            var kitchen = new Vector3(3f, 0f, -6f);
            HouseNpc npc = null;
            yield return WaitUntil(() =>
            {
                npc = ActiveNpcs().OrderBy(n => Vector3.Distance(n.transform.position, kitchen)).FirstOrDefault();
                return npc != null && NearestRoom(npc.transform.position) == "Kitchen";
            }, 60);
            if (npc == null) throw new InvalidOperationException("No houseguest in the kitchen.");
            yield return ApproachAndOpen(npc, shot);
            if (seasonDirector.Snapshot.phase == EpisodePhase.Social && HasSeasonButtonText(EpisodeHud.SmallTalkCaption))
                yield return ClickSeasonButton(EpisodeHud.SmallTalkCaption);
            if (NearestRoom(npc.transform.position) != "Kitchen") shot.reason = "the pair was not in the kitchen";
        }

        private IEnumerator NightConversation(LookShot shot)
        {
            yield return WaitUntil(() => seasonDirector.Snapshot.npcSocial.pending.Count > 0, 65);
            var pending = seasonDirector.Snapshot.npcSocial.pending.FirstOrDefault();
            if (pending == null) throw new InvalidOperationException("No houseguest conversation began within a minute.");
            var npc = ActiveNpcs().FirstOrDefault(n => n.Id == pending.firstId) ?? ActiveNpcs().FirstOrDefault(n => n.Id == pending.secondId);
            if (npc == null) throw new InvalidOperationException("The conversing houseguests are not in the house.");
            if (!FindSeasonNpcApproach(npc, out var approach)) throw new InvalidOperationException("No approach to the conversation.");
            seasonPlayer.TryMoveTo(approach);
            yield return WaitSeasonWalk("look conversation", approach, false);
            yield return WaitUntil(() => !string.IsNullOrEmpty(seasonDirector.ObservedNpcConversation), 55);
            shot.reason = "the bubbles and the night light are in; houseguests do not sleep yet";
        }

        private IEnumerator HouseEventChoice(LookShot shot)
        {
            yield return WaitUntil(() => HouseEvents.Pending(seasonDirector.Snapshot) != null, 30);
            if (HouseEvents.Pending(seasonDirector.Snapshot) == null)
            {
                // Stand where two houseguests are, which is what offers a proximity event.
                var pair = ActiveNpcs().GroupBy(n => NearestRoom(n.transform.position)).Where(g => g.Count() >= 2).FirstOrDefault();
                if (pair != null)
                {
                    var target = pair.First();
                    if (FindSeasonNpcApproach(target, out var approach) && seasonPlayer.TryMoveTo(approach))
                        yield return WaitSeasonWalk("look event", approach, false);
                    yield return WaitUntil(() => HouseEvents.Pending(seasonDirector.Snapshot) != null, 40);
                }
            }
            if (HouseEvents.Pending(seasonDirector.Snapshot) == null) throw new InvalidOperationException("No house event was offered during this capture route.");
            yield return OpenSeasonStation();
            shot.reason = "The event shows its involved houseguests and counts of known events. "
                + "Those counts describe activity, commitments and game stakes, not private NPC feelings.";
        }

        private IEnumerator CompetitionMidPlay(LookShot shot)
        {
            yield return AdvanceToPhase(state => EpisodeEngine.IsCompetition(state.phase) && !state.competitionResolved && EpisodeEngine.CompetitionPlayers(state).Any(c => c.isPlayer), 12);
            var state = seasonDirector.Snapshot;
            if (!EpisodeEngine.IsCompetition(state.phase)) throw new InvalidOperationException("No competition with the player was reached.");
            yield return OpenSeasonStation();
            var game = CompetitionMiniGames.For(EpisodeEngine.CompetitionCategory(state));
            yield return ClickSeasonButton(CompetitionMiniGames.EnterCaption(game));
            double prepareUntil = Time.realtimeSinceStartupAsDouble + 40;
            while (seasonDirector.IsChallengeActive && CompetitionSurface() != null && !CompetitionSurface().IsPlaying
                && Time.realtimeSinceStartupAsDouble < prepareUntil)
            {
                if (CompetitionSurface().Paused) yield return TryClickCompetitionControl(CompetitionSurface().IsAssembling ? "Pause assembly" : "Pause competition");
                else yield return null;
            }
            double until = Time.realtimeSinceStartupAsDouble + 3.5;
            int inputs = 0;
            while (Time.realtimeSinceStartupAsDouble < until && seasonDirector.IsChallengeActive)
            {
                if (CompetitionSurface() != null && CompetitionSurface().Paused)
                { yield return TryClickCompetitionControl(CompetitionSurface().IsAssembling ? "Pause assembly" : "Pause competition"); continue; }
                if (VisibleSeasonButtons().Any(b => b.name == "Reaction target"))
                { yield return TryClickCompetitionControl("Reaction target"); if(lastClickLanded)inputs++; }
                yield return null;
            }
            shot.reason = "The player and available cast reserve yard positions before the attempt. "
                + "Spectators use real loungers when available, with standing places otherwise; inspect visibility and body contact.";
        }

        private IEnumerator NominationDiscussion(LookShot shot)
        {
            yield return AdvanceToPhase(state => state.phase == EpisodePhase.Nomination || state.phase == EpisodePhase.Campaign, 12);
            var state = seasonDirector.Snapshot;
            if (state.phase == EpisodePhase.Nomination && state.hohId == state.playerId)
            {
                yield return OpenSeasonStation();
                foreach (var candidate in EpisodeEngine.NominationCandidates(state).Take(2))
                    yield return ClickSeasonButton(candidate.name, true);
                yield return ClickSeasonButton(EpisodeHud.ShowCandidateContextCaption, true);
                shot.reason = "Selected nominees expose their public competition record and the player's own relationship and promises.";
                yield break;
            }
            var npc = ActiveNpcs().FirstOrDefault();
            if (npc == null) throw new InvalidOperationException("Nobody to discuss nominations with.");
            yield return ApproachAndOpen(npc, shot);
            shot.reason = "The player is not HoH in this route; this frame shows the available conversation, not the nomination comparison.";
        }

        private IEnumerator KeyCeremonyShot(LookShot shot)
        {
            yield return AdvanceToPhase(state => state.phase == EpisodePhase.Nomination, 12);
            var state = seasonDirector.Snapshot;
            if (state.phase != EpisodePhase.Nomination) throw new InvalidOperationException("Nominations were not reached.");
            yield return OpenSeasonStation();
            if (state.hohId == state.playerId)
            {
                var candidates = EpisodeEngine.NominationCandidates(state).Take(2).Select(c => c.name).ToArray();
                foreach (var name in candidates) yield return ClickSeasonButton(name, true);
                yield return ClickSeasonButton("Commit nominations", true);
            }
            else yield return ClickSeasonButton("Continue episode", true);
            var ceremony = FindAnyObjectByType<KeyCeremony>();
            yield return WaitUntil(() => ceremony != null && ceremony.ShowingBlock, 12);
            if (ceremony == null || !ceremony.ShowingBlock) shot.reason = "the key ceremony card did not reach its block in time";
        }

        private IEnumerator EvictionBallot(LookShot shot)
        {
            // A ballot is a week away from the nomination the shot before it leaves behind: the veto
            // players, the veto, its meeting, the campaign and the speeches all come first, and each
            // is a step. Twenty was the count before those beats had commands of their own.
            yield return AdvanceToPhase(state => state.phase == EpisodePhase.Eviction
                && state.evictionStage == EvictionStage.Voting && EpisodeEngine.Voters(state).Any(v => v.id == state.playerId), 60);
            var state = seasonDirector.Snapshot;
            if (state.phase != EpisodePhase.Eviction) throw new InvalidOperationException("The eviction vote was not reached.");
            if (!seasonPlayer.TryMoveTo(seasonDirector.DiaryPosition)) throw new InvalidOperationException("The walk to the diary room was refused.");
            yield return WaitSeasonWalk("look ballot", seasonDirector.DiaryPosition, false);
            if (!seasonDirector.TryOpenDiary()) throw new InvalidOperationException("The diary room did not open for the ballot.");
            yield return WaitUntil(() => seasonDirector.IsDiarySettled, 25);
            if (!seasonDirector.IsDiarySettled) throw new TimeoutException("The diary ballot did not become ready after seating.");
            var nominee = state.Find(state.nominees.First());
            if (HasSeasonButtonText("Vote to evict " + nominee.name)) yield return ClickSeasonButton("Vote to evict " + nominee.name);
            yield return WaitForShot();
        }

        /// <summary>Plays the season forward with the walk's own decisions until <paramref name="reached"/>, or the step budget runs out.</summary>
        private IEnumerator AdvanceToPhase(Func<EpisodeState, bool> reached, int steps)
        {
            for (int i = 0; i < steps && !reached(seasonDirector.Snapshot); i++)
            {
                var recap = FindAnyObjectByType<WeeklyRecapScreen>();
                if (recap != null && recap.IsOpen) { yield return ClickAny(WeeklyRecapScreen.ContinueCaption); continue; }
                if (seasonDirector.IsFramingCeremony) { yield return WaitUntil(() => !seasonDirector.IsFramingCeremony, 8); continue; }
                var before = seasonDirector.Snapshot;
                var command = NextLookCommand(before);
                var result = seasonDirector.Submit(command);
                if (!result.accepted) throw new InvalidOperationException(before.phase + ": " + result.reason);
                yield return null; yield return null;
            }
        }

        /// <summary>The verifier's plain decisions: play nothing by hand, keep the season legal.</summary>
        private static EpisodeCommand NextLookCommand(EpisodeState state)
        {
            var command = new EpisodeCommand
            {
                id = Guid.NewGuid().ToString("N"), actorId = state.playerId,
                expectedPhase = state.phase, expectedRevision = state.revision, kind = EpisodeCommandKind.Advance,
            };
            if (state.pendingDiary != null && state.pendingDiary.week == state.week
                && (state.phase == EpisodePhase.Social || (state.phase == EpisodePhase.Eviction && state.evictionResolved)))
            {
                // A week does not begin its competition while a reflection is still waiting in the
                // diary room. The walk skips it rather than answering it: the sheet is photographing
                // rooms, not making the player's decisions for them.
                command.kind = EpisodeCommandKind.SkipDiary;
                command.targetId = state.pendingDiary.id;
            }
            else if (EpisodeEngine.IsCompetition(state.phase) && !state.competitionResolved)
            {
                command.kind = EpisodeCommandKind.Compete; command.performance = .5;
            }
            else if (state.phase == EpisodePhase.Nomination && state.hohId == state.playerId && state.nominees.Count == 0)
            {
                var picks = EpisodeEngine.NominationCandidates(state).Take(2).ToArray();
                command.kind = EpisodeCommandKind.Nominate; command.targetId = picks[0].id; command.secondTargetId = picks[1].id;
            }
            else if (state.phase == EpisodePhase.VetoMeeting && state.vetoHolderId == state.playerId && !state.vetoResolved)
            {
                command.kind = EpisodeCommandKind.ResolveVeto; command.useVeto = false;
            }
            else if (state.phase == EpisodePhase.VetoMeeting && !state.vetoResolved && state.hohId == state.playerId
                     && EpisodeEngine.NpcVetoSave(state) != null)
            {
                // Somebody else holds the veto and is going to use it, and the player is the Head of
                // Household: the engine will not advance past a meeting whose replacement nobody has
                // named. The walk names the one the house likes least, which is what an HoH with no
                // plan does and what Advance would have done on its own.
                command.kind = EpisodeCommandKind.ResolveVeto;
                command.useVeto = true;
                command.targetId = EpisodeEngine.NpcVetoSave(state);
                command.secondTargetId = EpisodeEngine.ReplacementCandidates(state)
                    .OrderBy(candidate => state.Score(state.hohId, candidate.id)).First().id;
            }
            else if (state.phase == EpisodePhase.Eviction && state.evictionStage == EvictionStage.Voting
                     && EpisodeEngine.Voters(state).Any(v => v.id == state.playerId) && !state.votes.Any(v => v.voterId == state.playerId))
            {
                command.kind = EpisodeCommandKind.CastVote; command.targetId = state.nominees.First();
            }
            else if (state.phase == EpisodePhase.FinalEviction && state.hohId == state.playerId)
            {
                command.kind = EpisodeCommandKind.FinalEvict; command.targetId = state.Active.First(c => c.id != state.playerId).id;
            }
            else if (state.phase == EpisodePhase.Jury && !state.Active.Any(c => c.id == state.playerId) && !state.votes.Any(v => v.voterId == state.playerId))
            {
                command.kind = EpisodeCommandKind.CastVote; command.targetId = state.Active.First().id;
            }
            return command;
        }
    }
}
