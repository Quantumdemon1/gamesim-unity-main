using System;
using System.Collections.Generic;
using System.Linq;
using Gamesim.House;
using Gamesim.Presentation;
using Gamesim.Simulation;
using UnityEngine;

namespace Gamesim.Episode
{
    /// <summary>
    /// Private-room presentation only. Drafts are transient; the existing episode engine remains
    /// the sole authority for nominations, veto decisions, ballots, and their consequences.
    /// </summary>
    public sealed partial class EpisodeDirector
    {
        private HouseRoomMarker diaryRoom;
        private bool diaryOpen;
        private DiaryDecisionDraft diaryDraft;
        private HouseInteractionAnchor diaryAnchor;
        private DiarySeatPose diarySeat;

        private sealed class DiaryDecisionDraft
        {
            public EpisodeState origin;
            public EpisodeCommandKind kind;
            public string target, second, summary;
            public bool useVeto;
        }

        public bool HasDiaryRoom => diaryRoom != null && diaryRoom.gameObject.activeInHierarchy
            && diaryRoom.gameObject.scene == gameObject.scene && diaryAnchor!=null && diaryAnchor.isActiveAndEnabled
            && diaryAnchor.Seated && diaryAnchor.RoomId=="Private" && diaryAnchor.gameObject.scene==gameObject.scene;
        public Vector3 DiaryPosition => HasDiaryRoom ? diaryAnchor.Approach : Vector3.positiveInfinity;
        public bool IsDiaryOpen => diaryOpen;
        public bool IsDiarySettled => diaryOpen && diarySeat!=null && diarySeat.IsSettled;
        public bool HasDiaryDecisionDraft => diaryDraft != null;
        public bool CanUseDiary => IsReady && !blockedRecovery && !challengeActive && playerIsActive
            && player != null && HasDiaryRoom && HasDiarySight();

        private void ResolveDiaryRoom()
        {
            // Scene-local lookup prevents a second loaded house from supplying the wrong marker.
            var rooms = gameObject.scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<HouseRoomMarker>(true))
                .Where(room => room.RoomName == "Private").ToArray();
            diaryRoom = rooms.Length == 1 ? rooms[0] : null;
            HouseInteractionAnchors.EnsureDefaults(gameObject.scene);
            HouseInteractionAnchors.TryFind(gameObject.scene,HouseInteractionAnchors.DiaryVenue,0,out diaryAnchor);
            if (diaryRoom == null)
                Debug.LogWarning("Gamesim diary room needs exactly one Private room marker in this episode scene. Diary interaction is disabled.", this);
        }

        private bool HasDiarySight()
        {
            var origin = player.transform.position + Vector3.up * 1.15f;
            var offset = DiaryPosition + Vector3.up * 1.15f - origin;
            float distance = offset.magnitude;
            if (distance > 2.8f) return false;
            if (distance < .01f) return true;
            int count = Physics.RaycastNonAlloc(origin, offset / distance, sightHits, distance,
                HouseLayers.Sight, QueryTriggerInteraction.Ignore);
            if (count == sightHits.Length) return false;
            for (int index = 0; index < count; index++)
            {
                var hit = sightHits[index].transform;
                if(diarySeat!=null && diarySeat.IsOccupying && diaryAnchor!=null
                    && hit.IsChildOf(diaryAnchor.transform.parent))continue;
                if (!hit.IsChildOf(player.transform) && !hit.IsChildOf(diaryRoom.transform)) return false;
            }
            return true;
        }

        public void GoToDiary()
        {
            if (!IsReady || blockedRecovery || !playerIsActive || !HasDiaryRoom) return;
            // Both errands, not just the station's: a pending click on a houseguest used to
            // survive the R key and pull the player back out of the diary trip.
            CancelTravel();
            ClosePanels();
            EndDiaryVisit(true);CloseHouseActivities(true);
            // Watched, as the trip to the episode screen is. This one used to leave the camera
            // wherever it was, and the player walked out of the frame towards a room the status
            // line had just told them to walk to.
            bool going = TryTravel(DiaryPosition);
            message = !going ? "The diary room is not reachable from here. Your current episode is unchanged."
                : LastTravel == TravelKind.Warp
                    ? "At the private room: press E to open your diary. No choice is committed by entering."
                    : "Walk to the private room, then press E to open your diary. No choice is committed by entering.";
            Render();
        }

        public bool TryOpenDiary()
        {
            if (!CanUseDiary) return false;
            if(diaryOpen)return true;
            PauseNpcSocialForPanel();
            if (blockedRecovery) return false;
            ClosePanels();EndDiaryVisit(true);CloseHouseActivities(true);diaryOpen = true;
            diaryTab = DiaryTab.Record; diaryTabChosen = false; diaryRulesOpen = false;
            // The line under the frame said how to get in here - "walk to the private room, then
            // press E" - for the whole visit. It says where the player is now.
            message = DiaryInsideMessage;
            player.SetInputEnabled(false); cameraRig.ControlsEnabled = false;
            // Physical arrival authorizes the visit. Seating is temporary presentation, restored
            // before movement resumes; the shot frames this player's face against the diary set.
            if(!FrameDiaryChair())
            {
                ClosePanels();
                message="The diary chair is unavailable. Your current episode is unchanged.";
                Render();return false;
            }
            Render(); return true;
        }

        /// <summary>The chair the diary's shot looks at, as the set names it.</summary>
        public const string DiaryChairName = HouseInteractionAnchors.DiaryProp;
        /// <summary>
        /// Legacy camera constants remain for downstream integrations. New framing reads the
        /// chair's camera anchor and follows the actual seated head through DiarySeatPose.
        /// </summary>
        public const float DiaryShotBehind = 1.0f;
        public const float DiaryShotAside = 0.5f;
        public const float DiaryShotEyeHeight = 1.9f;
        public const float DiaryShotLookHeight = 0.7f;
        public const float DiaryShotFieldOfView = 40f;
        public const float DiaryShotSeconds = 0.8f;
        public const float DiaryShotDepthOfField = 0.7f;

        /// <summary>
        /// Stages a seated confessional after arrival and frames the player's face. Navigation stays
        /// reserved at the approach. Reduced motion uses the same framing with an immediate cut.
        /// </summary>
        private bool FrameDiaryChair()
        {
            if (cameraRig == null || diaryAnchor==null || !diaryAnchor.isActiveAndEnabled) return false;
            diarySeat=player.GetComponent<DiarySeatPose>() ?? player.gameObject.AddComponent<DiarySeatPose>();
            if(!diarySeat.Begin(diaryAnchor,cameraRig,()=>this!=null && diaryOpen,()=>
            {
                if(this==null || !diaryOpen)return;
                ClosePanels();
                message="The diary visit ended because its chair became unavailable. Unconfirmed choices were discarded.";
                Render();
            },()=>{StartDiaryShot();Render();}))return false;
            return true;
        }

        private void StartDiaryShot()
        {
            if(!diaryOpen || diarySeat==null || !diarySeat.IsSettled || cameraRig==null)return;
            var eye=diaryAnchor.CameraPosition;
            var look=diarySeat.FacePosition-Vector3.up*.1f;
            var line = look - eye;
            float distance = line.magnitude;
            cameraRig.MoveTo(new HouseCameraRig.Shot
            {
                Focus = look, Distance = distance,
                Pitch = Mathf.Asin(Mathf.Clamp(-line.y / distance, -1f, 1f)) * Mathf.Rad2Deg,
                Yaw = Mathf.Atan2(line.x, line.z) * Mathf.Rad2Deg,
                FieldOfView = DiaryShotFieldOfView, Seconds = DiaryShotSeconds, DepthOfFieldWeight = DiaryShotDepthOfField,
            });
        }

        private void EndDiaryVisit(bool immediately=false)
        {
            if(diarySeat==null)return;
            if(immediately)diarySeat.End();else diarySeat.RequestExit();
        }

        public void CancelDiaryDecision()
        {
            CancelDiaryDecision(diaryDraft);
        }

        private void CancelDiaryDecision(DiaryDecisionDraft expected)
        {
            if (!diaryOpen || expected == null || !ReferenceEquals(diaryDraft, expected)) return;
            diaryDraft = null; message = "Unconfirmed diary choice discarded. The episode is unchanged.";
            Render();
        }

        public void ConfirmDiaryDecision()
        {
            ConfirmDiaryDecision(diaryDraft);
        }

        private void ConfirmDiaryDecision(DiaryDecisionDraft expected)
        {
            // A detached callback from an older panel cannot confirm a newer draft.
            if (!diaryOpen || expected == null || !ReferenceEquals(diaryDraft, expected)) return;
            if (!CanUseDiary) { ClosePanels(); return; }
            if(!IsDiarySettled)return;
            var decision = diaryDraft;
            diaryDraft = null; // Clear before Submit renders; a double click cannot submit a second command.
            if (!IsCurrentDiaryRevision(decision.origin))
            { message = "The episode changed. Review a new diary choice before confirming."; Render(); return; }
            Commit(decision.origin, decision.kind, decision.target, decision.second, decision.useVeto);
        }

        private bool IsCurrentDiaryRevision(EpisodeState state) => state != null && projected != null
            && state.sessionId == projected.sessionId && state.revision == projected.revision
            && state.phase == projected.phase && state.playerId == projected.playerId;

        /// <summary>Stages a private study choice only; the engine rolls and spends the action on confirmation.</summary>
        public void ReviewStudyHouse(string choiceId) => ReviewStudyHouse(projected, choiceId);

        private void ReviewStudyHouse(EpisodeState state, string choiceId)
        {
            if (!diaryOpen || !IsDiarySettled || diaryDraft != null || !CanUseDiary || !IsCurrentDiaryRevision(state) || state.phase != EpisodePhase.Social
                || state.pendingDiary != null
                || EpisodeEngine.SocialActionsSpent(state) >= EpisodeEngine.SocialActionBudget(state)) return;
            if (choiceId != "memorize-layout" && choiceId != "sneak-peek") return;
            bool memorize = choiceId == "memorize-layout";
            int chance = WebStudyHouse.SuccessChance(choiceId, null);
            diaryDraft = new DiaryDecisionDraft
            {
                origin = state, kind = EpisodeCommandKind.StudyHouse, target = choiceId,
                summary = (memorize ? "Memorize the layout." : "Sneak a peek at production notes.")
                    + "\nCost: 1 social action ("
                    + (EpisodeEngine.SocialActionBudget(state) - EpisodeEngine.SocialActionsSpent(state))
                    + " of " + EpisodeEngine.SocialActionBudget(state) + " remaining this week)."
                    + (memorize ? "\nGuaranteed +1 preparation, up to the limit of 5."
                        : "\nSuccess chance: " + chance + "%. Success adds 2 preparation; failure removes 1.")
                    + "\nPreparation stays between 0 and 5. Current preparation: " + state.playerStudyBonus + "/5."
                    + " Even at a limit, confirming uses one action."
                    + "\nPreparation is " + StudyUse(state) + "."
            };
            Render();
        }

        public void ReflectDiary(string choiceId)
        {
            if ((!diaryOpen && !phaseOpen) || projected?.pendingDiary == null) return;
            if (diaryOpen && (!CanUseDiary || !IsDiarySettled)) return;
            Commit(projected, EpisodeCommandKind.ReflectDiary, projected.pendingDiary.id, choiceId);
        }

        public void SkipDiary()
        {
            if ((!diaryOpen && !phaseOpen) || projected?.pendingDiary == null) return;
            if (diaryOpen && (!CanUseDiary || !IsDiarySettled)) return;
            Commit(projected, EpisodeCommandKind.SkipDiary, projected.pendingDiary.id);
        }

        private void OfferPlayerDecision(EpisodeState state, bool privateRoom, EpisodeCommandKind kind,
            string summary, string target = null, string second = null, bool useVeto = false)
        {
            if (!privateRoom) { Commit(state, kind, target, second, useVeto); return; }
            if (!diaryOpen || !IsDiarySettled || !CanUseDiary) return;
            // This is a presentation allow-list, not a parallel rules engine. Submit checks authority,
            // candidate validity, duplicate IDs, phase, and revision again against the current state.
            if (kind != EpisodeCommandKind.Nominate && kind != EpisodeCommandKind.ResolveVeto
                && !(kind == EpisodeCommandKind.CastVote && state.phase == EpisodePhase.Eviction)) return;
            diaryDraft = new DiaryDecisionDraft
            { origin = state, kind = kind, target = target, second = second, useVeto = useVeto, summary = summary };
            Render();
        }

        private void RenderDiary(EpisodeState state)
        {
            // A live ballot, or one waiting to be confirmed, is the eviction vote as mockup-08 draws
            // it: a panel across the top of the room, over the player in the chair. Every other
            // visit is the room's own page (mockup-11): a column of what the player can do here.
            bool ballotDraft = diaryDraft != null && diaryDraft.kind == EpisodeCommandKind.CastVote && state.phase == EpisodePhase.Eviction;
            bool ballot = IsDiarySettled && (ballotDraft || (diaryDraft == null && state.pendingDiary == null && BallotIsLive(state)));
            if (ballot) hud.SetActivityLayout(EpisodeHud.ActivityLayout.Ballot);
            else hud.SetDiaryRoomLayout();
            hud.DiaryHeader(ballot ? null : EpisodeHud.DiaryOptionsTitle);
            // Mockup-08's bar under the vote: the player, and the rule the vote is cast under - or,
            // while a choice waits to be confirmed, the rule the draft is held under.
            if (ballot)
                hud.SpeechBar(state.playerId, EpisodeHud.SelfTitle(state.Find(state.playerId)),
                    ballotDraft ? DraftBarLine
                    : EpisodeEngine.NeedsPlayerTieBreak(state)
                        ? "The vote is tied, and yours decides it."
                        : "One vote, cast in private. Nobody sees it, unless you tell them.", true);
            if(!IsDiarySettled)
            {
                hud.Aside("Your own memories, and the decisions that are yours to make.");
                hud.Paragraph("Walk to the chair, turn and sit. Your diary choices appear when you are seated.");
                return;
            }
            if (diaryDraft != null)
            {
                var reviewed = diaryDraft;
                bool reflection = diaryDraft.kind == EpisodeCommandKind.ReflectDiary;
                bool study = diaryDraft.kind == EpisodeCommandKind.StudyHouse;
                if (ballotDraft)
                {
                    // The ballot stays on screen while the choice waits to be confirmed, the chosen
                    // card marked - pressing the other card changes the choice, nothing is cast
                    // until Confirm. Confirm and the way back are the pinned pair under the cards,
                    // outside the scroll, where mockup-08 puts its button: in the column they
                    // followed the cards, and "Back to diary (discard choice)" stood past the
                    // panel's foot behind a scrollbar (ballot-review; UI-UX-PASS-PLAN T0). The
                    // review's summary is the ballot's line under its heading, the draft's rule is
                    // the bar's (DraftBarLine), and the cards take the room between.
                    bool tieBreak = EpisodeEngine.NeedsPlayerTieBreak(state);
                    hud.PinnedPair(EpisodeHud.DiaryConfirmCaption, () => ConfirmDiaryDecision(reviewed),
                        EpisodeHud.DiaryCancelCaption, () => CancelDiaryDecision(reviewed));
                    // What the ballot chosen would break of the player's word, over the cards
                    // (EpisodeDirector.YourWord).
                    hud.BallotCards(state, state.nominees, reviewed.target, id => id == reviewed.target ? "Your choice: " + state.Find(id).name : "Choose " + state.Find(id).name + " instead",
                        id => { if (id != reviewed.target) OfferBallot(state, true, tieBreak, id); },
                        line: reviewed.summary, warning: DraftWarning(reviewed), under: 0f);
                    return;
                }
                RenderDiaryReview(reviewed, study, reflection);
                return;
            }
            // A live ballot is the reason the player is in the chair, so it is the whole panel
            // (mockup-08), with what the room keeps under it.
            if (ballot)
            {
                RenderPlayerDecision(state, true);
                RenderDiaryRecord(state);
                RenderDiaryMemories(state);
                return;
            }
            // What the player has to talk about in here, as mockup-11 captions the chair: their
            // own latest memory under their name - one they may know (KnownBallots.PlayerMemories).
            var latest = KnownBallots.PlayerMemories(state).LastOrDefault();
            var self = state.Find(state.playerId);
            if (latest != null && self != null)
                hud.Confessional((self.name ?? "You").Split(' ')[0], latest.text);
            // Refinement Kit 6's three tabs, in place of one column that ran the decision, the
            // record's rules and every memory together. The decision's tab opens first when there
            // is something to choose; otherwise the record does. A tab is view state: choosing
            // one commits nothing.
            bool choice = DiaryHasChoice(state);
            var tab = diaryTabChosen ? diaryTab : choice ? DiaryTab.Pending : DiaryTab.Record;
            hud.FilterRow(EpisodeHud.DiaryTabsName, new List<(string, bool, Action)>
            {
                (DiaryRecordTabCaption, tab == DiaryTab.Record, () => ChooseDiaryTab(DiaryTab.Record)),
                (DiaryMemoriesTabCaption, tab == DiaryTab.Memories, () => ChooseDiaryTab(DiaryTab.Memories)),
                (DiaryPendingTabCaption, tab == DiaryTab.Pending, () => ChooseDiaryTab(DiaryTab.Pending)),
            });
            if (tab == DiaryTab.Record) RenderDiaryRecordTab(state, choice);
            else if (tab == DiaryTab.Memories) RenderDiaryMemories(state);
            else RenderDiaryPending(state);
        }

        public const string DiaryRecordTabCaption = "Your record";
        public const string DiaryMemoriesTabCaption = "Memories";
        public const string DiaryPendingTabCaption = "Pending decision";
        /// <summary>The line under the frame while the player is in the chair.</summary>
        public const string DiaryInsideMessage = "In the private diary room. Nothing here is committed until you confirm it.";

        private enum DiaryTab { Record, Memories, Pending }
        private DiaryTab diaryTab;
        private bool diaryTabChosen, diaryRulesOpen;

        private void ChooseDiaryTab(DiaryTab tab)
        {
            diaryTab = tab; diaryTabChosen = true;
            Render();
        }

        /// <summary>
        /// Whether the decision tab has something to choose: an answer owed, a ceremony decision
        /// of the player's own, or a way to study with an action left to study with. The same
        /// conditions <see cref="RenderPlayerDecision"/> and the study section draw under.
        /// </summary>
        private static bool DiaryHasChoice(EpisodeState state)
        {
            if (state.pendingDiary != null) return true;
            var me = state.Find(state.playerId);
            if (me == null || me.status != ContestantStatus.Active) return false;
            if (EpisodeEngine.OpenSummons(state) != null) return true;
            if (state.phase == EpisodePhase.Social
                && EpisodeEngine.SocialActionsSpent(state) < EpisodeEngine.SocialActionBudget(state)) return true;
            return HasPlayerDecision(state);
        }

        /// <summary>
        /// A beat of the episode screen with nothing to read or decide: the reflection prompt, and
        /// "Continue episode" under the house's status. It stays a card rather than a stage of
        /// empty space.
        /// </summary>
        private bool QuietBeat(EpisodeState state)
        {
            if (state.pendingDiary != null) return true;
            if (EpisodeEngine.OpenStoryBeats(state).Count > 0 && state.Find(state.playerId)?.status == ContestantStatus.Active) return false;
            if (challengeActive || EpisodeEngine.IsCompetition(state.phase)) return false;
            if (state.phase == EpisodePhase.Social || state.phase == EpisodePhase.Campaign
                || state.phase == EpisodePhase.JuryQuestioning || state.phase == EpisodePhase.FinalSpeeches
                || state.phase == EpisodePhase.Finished) return false;
            if (HasPlayerDecision(state)) return false;
            if (state.phase == EpisodePhase.FinalEviction && state.hohId == state.playerId) return false;
            if (state.phase == EpisodePhase.Jury && !state.Active.Any(c => c.isPlayer)
                && !state.votes.Any(v => v.voterId == state.playerId)) return false;
            return true;
        }

        private static bool HasPlayerDecision(EpisodeState state)
        {
            if (state.Find(state.playerId)?.status != ContestantStatus.Active) return false;
            if (state.phase == EpisodePhase.Nomination && state.nominees.Count == 0 && state.hohId == state.playerId) return true;
            if (state.phase == EpisodePhase.VetoMeeting && !state.vetoResolved)
            {
                if (state.vetoHolderId == state.playerId) return true;
                if (state.hohId == state.playerId && EpisodeEngine.NpcVetoSave(state) != null) return true;
            }
            if (state.phase == EpisodePhase.Eviction && state.evictionStage == EvictionStage.Speeches
                && state.nominees.Contains(state.playerId)
                && !state.evictionSpeeches.Any(speech => speech.speakerId == state.playerId)) return true;
            return BallotIsLive(state);
        }

        /// <summary>
        /// Your record (Kit 6's preview 05): whether anything waits for you, then the values the
        /// room keeps - preparation, persona, the jury's recorded impression - one a row, and the
        /// full rules behind a disclosure rather than in the reading column.
        /// </summary>
        /// <summary>
        /// Where study preparation counts, as the season's competition rules have it. From rules 3
        /// the engine adds it on every player entry route (EpisodeEngine.CommonCompetitionBonus):
        /// the weekly competitions however they are played, and all three parts of the final Head
        /// of Household. Before rules 3 only the weekly simulated option read it.
        /// </summary>
        public static string StudyUse(EpisodeState state) =>
            state != null && state.competitionRulesVersion >= 3
                ? "used in every competition you enter, however you play it, the final HoH's three parts included"
                : "used only by the weekly simulated HoH/Veto option, not precision play or final HoH";

        /// <summary>The same, in the record row's few words.</summary>
        public static string StudyUseShort(EpisodeState state) =>
            state != null && state.competitionRulesVersion >= 3
                ? "Used in every competition you enter."
                : "Used only by the weekly simulated HoH/Veto option.";

        private void RenderDiaryRecordTab(EpisodeState state, bool choice)
        {
            bool ballotCast = state.phase == EpisodePhase.Eviction && state.votes.Any(vote => vote.voterId == state.playerId);
            if (choice)
                hud.DiaryStatusCard("A private decision is waiting", "It is under " + DiaryPendingTabCaption + ". Nothing is committed until you confirm it.");
            else
                hud.DiaryStatusCard("No private decision pending", ballotCast
                    ? "Your ballot has already been recorded. Return to the episode screen for the eviction reveal."
                    : "Viewing this page changes nothing.");
            int reflections = state.playerPersona.history.Count;
            int jurors = state.jurySentiment.jurors.Count;
            hud.RecordSummary(new List<(string, string, string, string)>
            {
                ("Study preparation", PackArt.KitIconBook, state.playerStudyBonus + " / 5",
                    StudyUseShort(state)),
                ("Diary persona", PackArt.KitIconPerson, state.playerPersona.current,
                    reflections + (reflections == 1 ? " recorded reflection." : " recorded reflections.")),
                // A record of how the jury has read you, never a forecast of how it will vote; and
                // with no jury there is no record at all, which is not the same as a zero.
                ("Recorded jury impression", PackArt.KitIconJury,
                    jurors == 0 ? "None yet" : state.jurySentiment.overallSentiment.ToString("+0;-0;0"),
                    jurors == 0 ? "No one is on the jury yet, so nothing is recorded."
                        : "Across " + jurors + (jurors == 1 ? " juror" : " jurors") + ". Not a vote prediction."),
            });
            hud.Disclosure(EpisodeHud.DiaryRulesCaption, diaryRulesOpen, () => { diaryRulesOpen = !diaryRulesOpen; Render(); });
            if (!diaryRulesOpen) return;
            hud.Aside("Study preparation: saved between weeks; " + StudyUse(state) + ". It stays between 0 and 5 and changes only when you confirm a study approach.");
            hud.Aside("Diary persona: each confirmed reflection adds one to your persona history; your displayed persona may remain unchanged.");
            hud.Aside("Jury impression: an impression, not a promise. A reflection can shift it for each current juror, within the ledger's limits. It is not a directed-trust change or a jury ballot.");
        }

        /// <summary>Your memories, newest first: the room's own record of what you have seen.</summary>
        private void RenderDiaryMemories(EpisodeState state)
        {
            hud.DiarySection("YOUR PRIVATE REFLECTIONS");
            var memories = KnownBallots.PlayerMemories(state).Reverse().Take(20).ToArray();
            if (memories.Length == 0) hud.Aside("You have no recorded personal memories yet. Explore and talk to the housemates.");
            foreach (var memory in memories) hud.Aside("Week " + memory.week + ": " + memory.text);
        }

        /// <summary>
        /// The decision tab: an answer owed, the week's decision if it is yours, a way to study -
        /// or, with none of them, that plainly, and no decision invented to fill the space.
        /// </summary>
        private void RenderDiaryPending(EpisodeState state)
        {
            bool any = RenderDiaryReflection(state);
            // Production's call, or a story's Diary Room moment: answered here as well as on the
            // episode screen, because the room is where it was said to be.
            any |= PendingSummons(state);
            if (state.pendingDiary == null && HasPlayerDecision(state))
            {
                hud.DiarySection("YOUR PENDING DECISION");
                RenderPlayerDecision(state, true);
                hud.Aside("Choose an option to review it before confirming. Episode ceremonies continue only at the episode screen.");
                any = true;
            }
            any |= RenderStudyHouse(state);
            if (any) return;
            hud.DiaryNothingPending(state.phase == EpisodePhase.Eviction && state.votes.Any(vote => vote.voterId == state.playerId)
                ? "Your ballot has already been recorded. Return to the episode screen for the eviction reveal."
                : "You have no private decision to make right now. You can review your own memories or leave the room.");
        }

        /// <summary>
        /// A decision under review (Kit 6's preview 06): what is being decided in one card, what
        /// confirming it records in another - the controller's own words, line for line - then the
        /// reminder that nothing is saved yet and the two ways out.
        /// </summary>
        private void RenderDiaryReview(DiaryDecisionDraft reviewed, bool study, bool reflection)
        {
            var lines = (reviewed.summary ?? string.Empty).Split('\n');
            string headline;
            string[] effects;
            if (reflection && lines.Length > 1)
            {
                // "Record this private answer:" then the answer in quotes: the answer is the card.
                headline = "\u201c" + lines[1].Trim().Trim('"') + "\u201d";
                effects = lines.Skip(2).ToArray();
            }
            else
            {
                headline = lines[0];
                effects = lines.Skip(1).ToArray();
            }
            hud.ReviewCard(EpisodeHud.DiaryReviewAnswerName,
                study ? "STUDY APPROACH" : reflection ? "PRIVATE REFLECTION" : "YOUR DECISION",
                PackArt.KitIconNote, headline, reflection, null, "NOT YET SAVED");
            if (effects.Length > 0)
                hud.ReviewCard(EpisodeHud.DiaryReviewEffectsName, "WHAT CONFIRMATION RECORDS", null, null, false, effects);
            // A nomination or a veto under review, and what it would break of the player's word.
            string breach = DraftWarning(reviewed);
            if (breach != null) hud.NamedParagraph(EpisodeHud.BreachWarningName, breach, UiTheme.Warning);
            hud.InfoNote("Review note", "Nothing has been committed yet. Confirm once to save this decision, or go back to discard it.");
            hud.Action(study ? EpisodeHud.StudyConfirmCaption : reflection ? EpisodeHud.DiaryConfirmReflectionCaption : EpisodeHud.DiaryConfirmCaption,
                () => ConfirmDiaryDecision(reviewed));
            hud.Action(study ? EpisodeHud.StudyCancelCaption : reflection ? EpisodeHud.DiaryCancelReflectionCaption : EpisodeHud.DiaryCancelCaption,
                () => CancelDiaryDecision(reviewed));
        }

        private void RenderDiaryRecord(EpisodeState state)
        {
            hud.DiarySection("YOUR DIARY RECORD");
            hud.DiaryRecord(
                ("star", "Study preparation: " + state.playerStudyBonus + "/5",
                    "Saved between weeks; " + StudyUse(state) + "."),
                ("journal", "Current diary persona: " + state.playerPersona.current + ".",
                    "Recorded reflections: " + state.playerPersona.history.Count + "."),
                ("people", "How the jury has read you so far: " + state.jurySentiment.overallSentiment.ToString("0")
                    + " across " + state.jurySentiment.jurors.Count + " jurors.", "An impression, not a promise."));
        }

        /// <summary>The study section, in free time; whether it drew.</summary>
        private bool RenderStudyHouse(EpisodeState state)
        {
            if (state.phase != EpisodePhase.Social || state.Find(state.playerId)?.status != ContestantStatus.Active) return false;
            hud.DiarySection("STUDY THE HOUSE");
            // Where preparation stands, beside the ways to raise it.
            hud.Paragraph("Study preparation: " + state.playerStudyBonus + "/5");
            string cost = "Study here in the private room during free time. Each confirmed approach uses one of the "
                + EpisodeEngine.SocialActionBudget(state) + " social actions this week allows. "
                + "Opening, reading, and cancelling cost nothing.";
            if (state.pendingDiary != null)
            { hud.Paragraph("Answer or skip your pending private reflection before studying."); return true; }
            if (EpisodeEngine.SocialActionsSpent(state) >= EpisodeEngine.SocialActionBudget(state))
            { hud.Paragraph("No social actions remain in this window. Return to the episode screen when ready to continue."); return true; }
            hud.OptionCard(EpisodeHud.StudyMemorizeCaption, "Guaranteed +1 preparation, up to the limit of 5.", "house",
                () => ReviewStudyHouse(state, "memorize-layout"));
            hud.OptionCard(EpisodeHud.StudySneakCaption, WebStudyHouse.SuccessChance("sneak-peek", null)
                + "% success chance: +2 preparation on success, \u22121 on failure.", "eye",
                () => ReviewStudyHouse(state, "sneak-peek"));
            hud.Aside("Preparation stays between 0 and 5, and changes only when you confirm. " + cost);
            return true;
        }

        /// <summary>The reflection owed, if one is; whether it drew.</summary>
        private bool RenderDiaryReflection(EpisodeState state)
        {
            var prompt = EpisodeEngine.CurrentDiary(state);
            if (prompt == null) return false;
            hud.DiarySection("POST-EVICTION REFLECTION \u00b7 WEEK " + prompt.week);
            hud.Paragraph("After " + (state.Find(state.pendingDiary.evictedId)?.name ?? "a housemate")
                + "'s eviction, what do you want to record about your own response?");
            hud.Aside("Choose an answer to review before committing it privately. Nothing here claims another housemate's feelings or that you caused the result.");
            foreach (var choice in prompt.choices)
            {
                var selected = choice;
                // How exposed the answer leaves the player is the card's last line, in words and
                // its colour - never part of the caption, which is how the suite and a screen
                // reader identify the control. Same mistake as the social action categories.
                hud.OptionCard(choice.persona + " \u00b7 " + choice.text, null, PersonaGlyph(choice.persona),
                    () => ReviewDiaryReflection(state, selected.id), Exposure(choice), ExposureTint(choice));
            }
            hud.OptionCard(EpisodeHud.DiarySkipReflectionCaption, "Record nothing about this week.", "exit", SkipDiary);
            return true;
        }

        private void ReviewDiaryReflection(EpisodeState state, string choiceId)
        {
            if (!diaryOpen || !IsDiarySettled || !CanUseDiary || state.pendingDiary == null) return;
            var choice = EpisodeEngine.CurrentDiary(state)?.choices.FirstOrDefault(option => option.id == choiceId);
            if (choice == null) return;
            int delta = choice.effects.juryDelta.GetValueOrDefault();
            var consequence = delta == 0 ? "This choice does not shift the recorded jury-impression ledger."
                : "Recorded impression shift: " + delta.ToString("+0;-0;0")
                    + " for each current juror, within the ledger's limits. This is not a directed-trust change or a jury ballot.";
            diaryDraft = new DiaryDecisionDraft
            {
                origin = state, kind = EpisodeCommandKind.ReflectDiary, target = state.pendingDiary.id, second = choice.id,
                summary = "Record this private answer:\n\"" + choice.text + "\"\nAdds one " + choice.persona
                    + " reflection to your persona history; your displayed persona may remain unchanged.\n" + consequence
            };
            Render();
        }

        /// <summary>One renderer supplies the existing public screen and the private confirmation flow.</summary>
        /// <summary>
        /// How exposed a diary answer leaves the player, read off the answer's own committed
        /// effects.
        ///
        /// <para>The web build badges its confession choices Safe or Moderate. <b>Social actions
        /// here carry no risk concept at all</b>, so badging those would be inventing a mechanic
        /// rather than restyling one — but diary answers genuinely differ: some carry a jury
        /// penalty, and the jury decides the season. So the badge is derived from
        /// <c>juryDelta</c> rather than authored, which means it cannot drift away from what the
        /// answer actually does.</para>
        /// </summary>
        private static string Exposure(WebDiaryChoice choice)
        {
            int jury = choice?.effects?.juryDelta ?? 0;
            return jury < 0 ? "costs jury goodwill" : jury > 0 ? "wins jury goodwill" : "safe";
        }

        /// <summary>The exposure's colour: amber for a cost, green for a gain, the accent for safe.</summary>
        private static Color ExposureTint(WebDiaryChoice choice)
        {
            int jury = choice?.effects?.juryDelta ?? 0;
            return jury < 0 ? UiTheme.Joke : jury > 0 ? UiTheme.Allied : UiTheme.Accent;
        }

        /// <summary>The glyph an answer's card is drawn with, from the persona it records.</summary>
        private static string PersonaGlyph(string persona)
        {
            switch ((persona ?? string.Empty).ToLowerInvariant())
            {
                case "ruthless": return "target";
                case "remorseful": return "mood-sad";
                case "calculated": return "journal";
                default: return "mood-neutral";
            }
        }

        /// <summary>Whose thoughts the voter roster is currently showing, if any.</summary>
        private string thoughtsVoterId;

        /// <summary>The voters' row of chips, so a test can find it the way it finds the diary's tabs.</summary>
        public const string VotersRowName = "Voters";

        /// <summary>The bar's line while a ballot waits to be confirmed: the rule the draft is held under.</summary>
        public const string DraftBarLine = "Nothing has been committed yet. Confirm once to save this decision, or go back to discard it.";

        /// <summary>
        /// The roster of eligible voters, with a per-voter reveal — the web build's "Thoughts"
        /// control, and the progress line that goes with it.
        ///
        /// <para><b>What it will not do is tell you how anyone is voting.</b> The web shows this
        /// before the ballots are in, and the obvious reading of "thoughts" would be the voter's
        /// leaning — but that is exactly the private coordination this project keeps out of every
        /// player-visible surface, and a knowledge-boundary check asserts its absence. Revealing it
        /// would also remove the reason the eviction reveal exists.</para>
        ///
        /// <para>So the reveal is built only from what the player already holds: their own trust in
        /// that houseguest, which the notebook prints, and the most recent thing the player
        /// themselves remembers about them. Nothing here is knowledge the player did not already
        /// have — it is the same information, gathered to where the decision is being made. Nor does
        /// it say who has voted: whose ballot is in the box is the box's (UI-UX-PASS-PLAN B0).</para>
        /// </summary>
        private void VoterRoster(EpisodeState state)
        {
            var voters = EpisodeEngine.Voters(state).ToArray();
            if (voters.Length == 0) return;

            hud.Heading("VOTERS");

            // One row of chips, not a row of the column a voter: at 68 tall each the roster ran the
            // ballot past the panel's foot behind a scrollbar in a house of six (ballot-diary;
            // UI-UX-PASS-PLAN T0). Each chip is the control it was. The caption is fixed whatever
            // the state: a control whose name changes as you use it is a control neither a test nor
            // a screen reader can refer to twice. The player's own entry is the row's summary, at
            // its end; the chosen voter's thoughts follow the row.
            var chips = new List<(string Caption, bool Active, Action Choose)>();
            string you = null;
            foreach (var voter in voters)
            {
                var actor = voter;
                if (actor.isPlayer)
                {
                    bool voted = state.votes.Any(vote => vote.voterId == actor.id);
                    you = HudPrimitives.WithYou(actor.name, true) + (voted ? "  ·  voted" : "");
                    continue;
                }
                chips.Add(("Thoughts · " + actor.name, thoughtsVoterId == actor.id,
                    () => { thoughtsVoterId = thoughtsVoterId == actor.id ? null : actor.id; Render(); }));
            }
            hud.FilterRow(VotersRowName, chips, you);
            var chosen = thoughtsVoterId != null ? voters.FirstOrDefault(voter => voter.id == thoughtsVoterId && !voter.isPlayer) : null;
            if (chosen != null) hud.Paragraph(Thoughts(state, chosen));
        }

        /// <summary>
        /// What the player knows about one voter, in their own words where they have any.
        /// Strictly the player's own knowledge: their trust, and their own memories.
        /// </summary>
        private static string Thoughts(EpisodeState state, ContestantState voter)
        {
            double trust = state.Score(state.playerId, voter.id);
            string standing = trust >= 25 ? "You trust " + voter.name + "."
                : trust <= -25 ? "There is bad blood between you and " + voter.name + "."
                : "You and " + voter.name + " are on level terms.";

            // A memory of a vote deal's or a vote promise's ending tells the ballot that ended it:
            // left out while that ballot is not the player's to know (KnownBallots; decision 4).
            var remembered = KnownBallots.PlayerMemories(state)
                .Where(memory => memory.subjectId == voter.id)
                .OrderByDescending(memory => memory.week)
                .FirstOrDefault();

            string recalled = remembered == null
                ? "You have nothing specific on them this season."
                : "Week " + remembered.week + ": " + remembered.text;

            bool allied = state.Allied(state.playerId, voter.id);
            return standing + " " + recalled
                + (allied ? " You are in an alliance together." : string.Empty)
                + "  (Your trust: " + trust.ToString("0") + ". How they vote is theirs, unless they tell you.)";
        }

        private bool RenderPlayerDecision(EpisodeState state, bool privateRoom)
        {
            if (state.Find(state.playerId)?.status != ContestantStatus.Active) return false;
            if (state.phase == EpisodePhase.Nomination && state.nominees.Count == 0 && state.hohId == state.playerId)
            {
                // Out of the diary, the decision is a band across the house (mockup-09); in the
                // diary it keeps the diary's own column.
                if (!privateRoom) hud.SetActivityLayout(EpisodeHud.ActivityLayout.Nominations);
                hud.Paragraph("You are HoH. Choose two different nominees. Commit only when both choices are correct.");
                if (!string.IsNullOrEmpty(state.backdoorTargetId))
                    hud.Paragraph("This week is aimed at " + state.Find(state.backdoorTargetId).name
                        + ". Nominate two others and use the veto to put them up.");
                var candidates = EpisodeEngine.NominationCandidates(state).Select(c => new EpisodeHud.Option(c.id, c.name)).ToArray();
                hud.ChooseNominationPair(state, candidates, (first, second) =>
                {
                    if (privateRoom && (first == second || !candidates.Any(option => option.Id == first)
                        || !candidates.Any(option => option.Id == second)))
                    { message = "Choose two different eligible nominees before reviewing the decision."; Render(); return; }
                    OfferPlayerDecision(state, privateRoom, EpisodeCommandKind.Nominate,
                        "Nominate " + state.Find(first)?.name + " and " + state.Find(second)?.name + ". Confirmed nominations become part of the episode record.", first, second);
                }, privateRoom ? EpisodeHud.DiaryReviewNominationsCaption : "Commit nominations");
                // The week's real target, after the decision it would shape rather than before the
                // cards: it is the optional move, and the cards are the one that has to be made.
                // Costs nothing and moves nobody: it is a plan, and the house cannot hear a plan.
                if (string.IsNullOrEmpty(state.backdoorTargetId))
                {
                    hud.Heading("A BACKDOOR PLAN");
                    hud.Paragraph("You can also settle on who this week is really aimed at. A backdoor plan "
                        + "costs nothing, tells nobody, and is yours to change until you nominate.");
                    foreach (var aim in EpisodeEngine.NominationCandidates(state))
                    {
                        string id = aim.id;
                        hud.Tag(hud.ActionFor(id, "Aim this week at " + aim.name,
                            () => Commit(state, EpisodeCommandKind.SetBackdoorPlan, id)),
                            Category(EpisodeCommandKind.SetBackdoorPlan));
                    }
                }
                return true;
            }
            if (state.phase == EpisodePhase.VetoMeeting && !state.vetoResolved
                && (state.vetoHolderId == state.playerId || state.hohId == state.playerId))
            {
                // On the episode screen the meeting is a screen of its own (EpisodeDirector.VetoMeeting,
                // PACK8-PASS-PLAN B3); the diary keeps its column of rows, below.
                if (!privateRoom) return VetoMeetingDecision(state);
                if (state.vetoHolderId == state.playerId)
                {
                    hud.Action("Do not use the veto", () => OfferPlayerDecision(state, privateRoom,
                        EpisodeCommandKind.ResolveVeto, "Decline to use the veto. Both current nominees remain nominated."));
                    if (EpisodeEngine.VetoIsLockedAtFinalFour(state)) { hud.Paragraph(VetoLockedLine); return true; }
                    if (!EpisodeEngine.ReplacementCandidates(state).Any()) { hud.Paragraph(VetoNoReplacementLine); return true; }
                    foreach (var nominee in state.nominees)
                    {
                        string saved = nominee;
                        if (state.hohId == state.playerId) VetoReplacements(state, saved, privateRoom);
                        else hud.PairedActionFor(null, saved, "Save " + state.Find(saved).name + " (HoH chooses replacement)", () =>
                            OfferPlayerDecision(state, privateRoom, EpisodeCommandKind.ResolveVeto,
                                "Use the veto to save " + state.Find(saved).name + ". The HoH chooses the replacement.", saved, useVeto: true));
                    }
                    return true;
                }
                var savedByNpc = EpisodeEngine.NpcVetoSave(state);
                if (savedByNpc != null) { VetoReplacements(state, savedByNpc, privateRoom); return true; }
            }
            if (state.phase == EpisodePhase.Eviction && state.evictionStage == EvictionStage.Speeches
                && state.nominees.Contains(state.playerId)
                && !state.evictionSpeeches.Any(speech => speech.speakerId == state.playerId))
            {
                hud.Heading("YOUR SPEECH FROM THE BLOCK");
                hud.Paragraph("The house votes after this. Say what you want them to have heard, or say nothing — "
                    + "an empty speech is a choice the house will read too.");
                hud.EvictionSpeech(SubmitEvictionSpeech);
                return true;
            }
            if (BallotIsLive(state))
            {
                bool tieBreak = EpisodeEngine.NeedsPlayerTieBreak(state);
                // Under the cards, where mockup-08 keeps its line: ahead of the title it read as
                // the page's heading. Built first, with the roster, so the cards can be sized to the
                // room the lines leave them, and moved under the cards by BallotCards (over).
                int over = hud.ColumnRows;
                hud.Paragraph(tieBreak ? "The vote is tied. As HoH, you cast the deciding vote." : "Your ballot stays yours. Only the count is read.");
                // At four there is exactly one eligible voter. That has always been true by
                // arithmetic and has never been said, which makes a sole ballot look like a bug.
                if (!tieBreak && EpisodeEngine.Voters(state).Count() == 1)
                    hud.Paragraph("At the final four only one houseguest votes, and tonight that is you. "
                        + "Your single vote decides the eviction outright.");
                VoterRoster(state);
                // Out of the diary a card is the vote itself, cast as it is pressed: what each one
                // would break of the player's word is said over the cards, before either is
                // pressed (EpisodeDirector.YourWord). In the diary the card stages the choice, and
                // its review says it then.
                hud.BallotCards(state, state.nominees, null, id => "Vote to evict " + state.Find(id).name,
                    id => OfferBallot(state, privateRoom, tieBreak, id), warning: privateRoom ? null : BallotWarning(state, state.nominees), over: over);
                return true;
            }
            return false;
        }

        /// <summary>Whether the player has an eviction ballot to cast right now.</summary>
        private static bool BallotIsLive(EpisodeState state) =>
            state.Find(state.playerId)?.status == ContestantStatus.Active
            && state.phase == EpisodePhase.Eviction && !state.evictionResolved
            && (state.evictionStage == EvictionStage.Voting || state.evictionStage == EvictionStage.Tiebreaker)
            && !state.votes.Any(vote => vote.voterId == state.playerId)
            && (EpisodeEngine.Voters(state).Any(voter => voter.isPlayer) || EpisodeEngine.NeedsPlayerTieBreak(state));

        private void OfferBallot(EpisodeState state, bool privateRoom, bool tieBreak, string id) =>
            OfferPlayerDecision(state, privateRoom, EpisodeCommandKind.CastVote,
                (tieBreak ? "Cast the deciding vote to evict " : "Vote privately to evict ")
                + state.Find(id).name + ". This records your ballot only; the eviction reveal happens at the episode screen.", id);

        private void VetoReplacements(EpisodeState state, string saved, bool privateRoom)
        {
            // The Head of Household naming the replacement for a houseguest's save is told whose
            // save it is: "Save X and nominate:" read as if the player were saving X.
            hud.Heading(state.vetoHolderId == state.playerId ? "Save " + state.Find(saved).name + " and nominate:"
                : VetoReplacementLine(state, saved));
            var replacements = privateRoom ? null : hud.Pairs();
            foreach (var candidate in EpisodeEngine.ReplacementCandidates(state))
            {
                string id = candidate.id;
                hud.PairedActionFor(replacements, id, candidate.name, () => OfferPlayerDecision(state, privateRoom, EpisodeCommandKind.ResolveVeto,
                    "Use the veto to save " + state.Find(saved).name + " and nominate " + state.Find(id).name + " as the replacement.", saved, id, true));
            }
        }
    }
}
