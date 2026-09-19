using System;
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

        private sealed class DiaryDecisionDraft
        {
            public EpisodeState origin;
            public EpisodeCommandKind kind;
            public string target, second, summary;
            public bool useVeto;
        }

        public bool HasDiaryRoom => diaryRoom != null && diaryRoom.gameObject.activeInHierarchy
            && diaryRoom.gameObject.scene == gameObject.scene;
        public Vector3 DiaryPosition => HasDiaryRoom ? diaryRoom.transform.position : Vector3.positiveInfinity;
        public bool IsDiaryOpen => diaryOpen;
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
            if (diaryRoom == null)
                Debug.LogWarning("Gamesim diary room needs exactly one Private room marker in this episode scene. Diary interaction is disabled.", this);
        }

        private bool HasDiarySight()
        {
            var origin = player.transform.position + Vector3.up * 1.15f;
            var offset = diaryRoom.transform.position + Vector3.up * 1.15f - origin;
            float distance = offset.magnitude;
            if (distance > 2.8f) return false;
            if (distance < .01f) return true;
            int count = Physics.RaycastNonAlloc(origin, offset / distance, sightHits, distance,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            if (count == sightHits.Length) return false;
            for (int index = 0; index < count; index++)
            {
                var hit = sightHits[index].transform;
                if (!hit.IsChildOf(player.transform) && !hit.IsChildOf(diaryRoom.transform)) return false;
            }
            return true;
        }

        public void GoToDiary()
        {
            if (!IsReady || blockedRecovery || !playerIsActive || !HasDiaryRoom) return;
            ClosePanels();
            message = player.TryMoveTo(DiaryPosition)
                ? "Walk to the private room, then press E to open your diary. No choice is committed by entering."
                : "The diary room is not reachable from here. Your current episode is unchanged.";
            Render();
        }

        public bool TryOpenDiary()
        {
            if (!CanUseDiary) return false;
            PauseNpcSocialForPanel();
            if (blockedRecovery) return false;
            ClosePanels(); diaryOpen = true;
            player.SetInputEnabled(false); cameraRig.ControlsEnabled = false;
            // A solitary room does not borrow an NPC's conversation framing or invent a camera
            // subject: its shot is the chair, over the player's shoulder.
            FrameDiaryChair();
            Render(); return true;
        }

        /// <summary>The chair the diary's shot looks at, as the set names it.</summary>
        public const string DiaryChairName = "Confessional chair A";
        /// <summary>
        /// The diary's chair shot (V5): the eye a metre behind the player and half a metre to one
        /// side, at standing height, looking at the chair - so the player's shoulder holds the edge
        /// of the frame and the chair the middle, with the room soft behind it.
        /// </summary>
        public const float DiaryShotBehind = 1.0f;
        public const float DiaryShotAside = 0.5f;
        public const float DiaryShotEyeHeight = 1.9f;
        public const float DiaryShotLookHeight = 0.7f;
        public const float DiaryShotFieldOfView = 40f;
        public const float DiaryShotSeconds = 0.8f;
        public const float DiaryShotDepthOfField = 0.7f;

        /// <summary>
        /// Frames the confessional chair from behind the player. The player is not moved: the room
        /// closes the moment they leave its reach, so warping them into the chair would close it.
        /// Reduced motion keeps the shot the viewer has, as it does for conversations. A house
        /// without the chair - the prototype scene - keeps the dollhouse view.
        /// </summary>
        private void FrameDiaryChair()
        {
            if (cameraRig == null || cameraRig.ReducedMotion) return;
            var chair = gameObject.scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<Transform>(true))
                .FirstOrDefault(t => t.name == DiaryChairName);
            if (chair == null) return;
            var from = player.transform.position;
            var toward = chair.position - from;
            toward.y = 0f;
            if (toward.sqrMagnitude < 0.0001f) return;
            toward.Normalize();
            var side = new Vector3(toward.z, 0f, -toward.x);
            // The eye and what it looks at, then the rig's terms for that line: the pivot is the
            // look point, the boom is the line's length, and the angles are the line's.
            var eye = from - toward * DiaryShotBehind + side * DiaryShotAside + Vector3.up * DiaryShotEyeHeight;
            var look = chair.position + Vector3.up * DiaryShotLookHeight;
            var line = look - eye;
            float distance = line.magnitude;
            cameraRig.MoveTo(new HouseCameraRig.Shot
            {
                Focus = look, Distance = distance,
                Pitch = Mathf.Asin(Mathf.Clamp(-line.y / distance, -1f, 1f)) * Mathf.Rad2Deg,
                Yaw = Mathf.Atan2(line.x, line.z) * Mathf.Rad2Deg,
                FieldOfView = DiaryShotFieldOfView, Seconds = DiaryShotSeconds, DepthOfFieldWeight = DiaryShotDepthOfField,
            });
            player.GetComponent<CharacterPresentation>()?.SetFacing(Mathf.Atan2(toward.x, toward.z) * Mathf.Rad2Deg);
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
            if (!diaryOpen || diaryDraft != null || !CanUseDiary || !IsCurrentDiaryRevision(state) || state.phase != EpisodePhase.Social
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
                    + "\nThis helps only the weekly simulated HoH/Veto option. It does not boost precision play or final HoH."
            };
            Render();
        }

        public void ReflectDiary(string choiceId)
        {
            if ((!diaryOpen && !phaseOpen) || projected?.pendingDiary == null) return;
            if (diaryOpen && !CanUseDiary) return;
            Commit(projected, EpisodeCommandKind.ReflectDiary, projected.pendingDiary.id, choiceId);
        }

        public void SkipDiary()
        {
            if ((!diaryOpen && !phaseOpen) || projected?.pendingDiary == null) return;
            if (diaryOpen && !CanUseDiary) return;
            Commit(projected, EpisodeCommandKind.SkipDiary, projected.pendingDiary.id);
        }

        private void OfferPlayerDecision(EpisodeState state, bool privateRoom, EpisodeCommandKind kind,
            string summary, string target = null, string second = null, bool useVeto = false)
        {
            if (!privateRoom) { Commit(state, kind, target, second, useVeto); return; }
            if (!diaryOpen || !CanUseDiary) return;
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
            hud.PanelTitle("PRIVATE DIARY ROOM", "Only your character's own memories and assigned decisions appear here.");
            if (diaryDraft != null)
            {
                var reviewed = diaryDraft;
                bool reflection = diaryDraft.kind == EpisodeCommandKind.ReflectDiary;
                bool study = diaryDraft.kind == EpisodeCommandKind.StudyHouse;
                hud.Heading(study ? "REVIEW YOUR STUDY APPROACH" : reflection ? "REVIEW YOUR PRIVATE ANSWER" : "REVIEW YOUR DECISION");
                hud.Paragraph(diaryDraft.summary);
                hud.Paragraph("Nothing has been committed yet. Confirm once to save this decision, or go back to discard it.");
                hud.Action(study ? EpisodeHud.StudyConfirmCaption : reflection ? EpisodeHud.DiaryConfirmReflectionCaption : EpisodeHud.DiaryConfirmCaption,
                    () => ConfirmDiaryDecision(reviewed));
                hud.Action(study ? EpisodeHud.StudyCancelCaption : reflection ? EpisodeHud.DiaryCancelReflectionCaption : EpisodeHud.DiaryCancelCaption,
                    () => CancelDiaryDecision(reviewed));
                return;
            }
            hud.Paragraph("A quiet place to reflect. Reading here does not change your mood, traits, relationships, or jury standing.");
            RenderDiaryRecord(state);
            RenderDiaryReflection(state);
            RenderStudyHouse(state);
            if (state.pendingDiary == null)
            {
                hud.Heading("YOUR PENDING DECISION");
                hud.Paragraph("Choose an option to review it before confirming. Episode ceremonies continue only at the episode screen.");
                if (!RenderPlayerDecision(state, true))
                    hud.Paragraph(state.phase == EpisodePhase.Eviction && state.votes.Any(vote => vote.voterId == state.playerId)
                        ? "Your ballot has already been recorded. Return to the episode screen for the eviction reveal."
                        : "You have no private decision to make right now. You can review your own memories or leave the room.");
            }
            hud.Heading("YOUR PRIVATE REFLECTIONS");
            var memories = state.memories.Where(memory => memory.ownerId == state.playerId).Reverse().Take(20).ToArray();
            if (memories.Length == 0) hud.Paragraph("You have no recorded personal memories yet. Explore and talk to the housemates.");
            foreach (var memory in memories) hud.Paragraph("Week " + memory.week + ": " + memory.text);
            hud.Paragraph("These are your recorded experiences, not access to another housemate's private thoughts.");
        }

        private void RenderDiaryRecord(EpisodeState state)
        {
            hud.Heading("YOUR DIARY RECORD");
            hud.Paragraph("Study preparation: " + state.playerStudyBonus + "/5. Saved between weeks; used only by the weekly simulated HoH/Veto option, not precision play or final HoH.");
            hud.Paragraph("Current diary persona: " + state.playerPersona.current + ". Recorded reflections: " + state.playerPersona.history.Count + ".");
            hud.Paragraph("Recorded jury-impression ledger: " + state.jurySentiment.overallSentiment.ToString("0")
                + " across " + state.jurySentiment.jurors.Count + " jurors. This is a gameplay record, not access to private thoughts or a forecast of votes.");
            hud.Paragraph("The impression ledger does not directly set jury ballots. No social or competition reward is applied to normal play by this display.");
        }

        private void RenderStudyHouse(EpisodeState state)
        {
            if (state.phase != EpisodePhase.Social || state.Find(state.playerId)?.status != ContestantStatus.Active) return;
            hud.Heading("STUDY THE HOUSE");
            hud.Paragraph("Study here in the private room during free time. Each confirmed approach uses one of the "
                + EpisodeEngine.SocialActionBudget(state) + " social actions this week allows. "
                + "Opening, reading, and cancelling cost nothing.");
            if (state.pendingDiary != null)
            { hud.Paragraph("Answer or skip your pending private reflection before studying."); return; }
            if (EpisodeEngine.SocialActionsSpent(state) >= EpisodeEngine.SocialActionBudget(state))
            { hud.Paragraph("No social actions remain in this window. Return to the episode screen when ready to continue."); return; }
            hud.Paragraph("Memorize: Guaranteed +1 preparation, up to the limit of 5. Sneak: "
                + WebStudyHouse.SuccessChance("sneak-peek", null)
                + "% success chance; +2 on success, −1 on failure. Preparation stays between 0 and 5.");
            hud.Paragraph("Choose an approach to review it. Your preparation changes only when you confirm.");
            hud.Action(EpisodeHud.StudyMemorizeCaption, () => ReviewStudyHouse(state, "memorize-layout"));
            hud.Action(EpisodeHud.StudySneakCaption, () => ReviewStudyHouse(state, "sneak-peek"));
        }

        private void RenderDiaryReflection(EpisodeState state)
        {
            var prompt = EpisodeEngine.CurrentDiary(state);
            if (prompt == null) return;
            hud.Heading("POST-EVICTION REFLECTION · WEEK " + prompt.week);
            hud.Paragraph("After " + (state.Find(state.pendingDiary.evictedId)?.name ?? "a housemate")
                + "'s eviction, what do you want to record about your own response?");
            hud.Paragraph("Choose an answer to review before committing it privately. Nothing here claims another housemate's feelings or that you caused the result.");
            foreach (var choice in prompt.choices)
            {
                var selected = choice;
                // The exposure badge is a chip on the control, not part of its caption: the
                // caption is how the suite and a screen reader identify a button, and appending to
                // it renamed every diary choice. Same mistake as the social action categories.
                hud.Tag(hud.Action(choice.persona + " · " + choice.text,
                    () => ReviewDiaryReflection(state, selected.id)), Exposure(choice));
            }
            hud.Action(EpisodeHud.DiarySkipReflectionCaption, SkipDiary);
        }

        private void ReviewDiaryReflection(EpisodeState state, string choiceId)
        {
            if (!diaryOpen || !CanUseDiary || state.pendingDiary == null) return;
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

        /// <summary>Whose thoughts the voter roster is currently showing, if any.</summary>
        private string thoughtsVoterId;

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
        /// have — it is the same information, gathered to where the decision is being made.</para>
        /// </summary>
        private void VoterRoster(EpisodeState state)
        {
            var voters = EpisodeEngine.Voters(state).ToArray();
            if (voters.Length == 0) return;

            int cast = voters.Count(voter => state.votes.Any(vote => vote.voterId == voter.id));
            hud.Heading("VOTERS  ·  " + cast + " OF " + voters.Length + " VOTED");

            foreach (var voter in voters)
            {
                var actor = voter;
                bool voted = state.votes.Any(vote => vote.voterId == actor.id);

                if (actor.isPlayer)
                { hud.Paragraph(actor.name + " (You)" + (voted ? "  ·  voted" : "")); continue; }

                // The caption is fixed whatever the state, and the "voted" marker is a chip rather
                // than part of it. A control whose name changes as you use it is a control neither
                // a test nor a screen reader can refer to twice.
                hud.Tag(hud.ActionFor(actor.id + ":thoughts", "Thoughts · " + actor.name,
                        () => { thoughtsVoterId = thoughtsVoterId == actor.id ? null : actor.id; Render(); }),
                    voted ? "voted" : null);

                if (thoughtsVoterId == actor.id) hud.Paragraph(Thoughts(state, actor));
            }
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

            var remembered = state.memories
                .Where(memory => memory.ownerId == state.playerId && memory.subjectId == voter.id)
                .OrderByDescending(memory => memory.week)
                .FirstOrDefault();

            string recalled = remembered == null
                ? "You have nothing specific on them this season."
                : "Week " + remembered.week + ": " + remembered.text;

            bool allied = state.Allied(state.playerId, voter.id);
            return standing + " " + recalled
                + (allied ? " You are in an alliance together." : string.Empty)
                + "  (Your trust: " + trust.ToString("0") + ". How they vote is theirs until the reveal.)";
        }

        private bool RenderPlayerDecision(EpisodeState state, bool privateRoom)
        {
            if (state.Find(state.playerId)?.status != ContestantStatus.Active) return false;
            if (state.phase == EpisodePhase.Nomination && state.nominees.Count == 0 && state.hohId == state.playerId)
            {
                hud.Paragraph("You are HoH. Choose two different nominees. Commit only when both choices are correct.");
                // The week's real target, named before the two people who will stand in for it.
                // Costs nothing and moves nobody: it is a plan, and the house cannot hear a plan.
                if (string.IsNullOrEmpty(state.backdoorTargetId))
                {
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
                else hud.Paragraph("This week is aimed at " + state.Find(state.backdoorTargetId).name
                    + ". Nominate two others and use the veto to put them up.");
                var candidates = EpisodeEngine.NominationCandidates(state).Select(c => new EpisodeHud.Option(c.id, c.name)).ToArray();
                hud.ChoosePair(candidates, (first, second) =>
                {
                    if (privateRoom && (first == second || !candidates.Any(option => option.Id == first)
                        || !candidates.Any(option => option.Id == second)))
                    { message = "Choose two different eligible nominees before reviewing the decision."; Render(); return; }
                    OfferPlayerDecision(state, privateRoom, EpisodeCommandKind.Nominate,
                        "Nominate " + state.Find(first)?.name + " and " + state.Find(second)?.name + ". Confirmed nominations become part of the episode record.", first, second);
                }, privateRoom ? EpisodeHud.DiaryReviewNominationsCaption : "Commit nominations");
                return true;
            }
            if (state.phase == EpisodePhase.VetoMeeting && !state.vetoResolved
                && (state.vetoHolderId == state.playerId || state.hohId == state.playerId))
            {
                if (state.vetoHolderId == state.playerId)
                {
                    hud.Action("Do not use the veto", () => OfferPlayerDecision(state, privateRoom,
                        EpisodeCommandKind.ResolveVeto, "Decline to use the veto. Both current nominees remain nominated."));
                    if (EpisodeEngine.VetoIsLockedAtFinalFour(state))
                    {
                        hud.Paragraph("At the final four a veto holder who is not on the block cannot use the veto. "
                            + "Nominations stand.");
                        return true;
                    }
                    if (!EpisodeEngine.ReplacementCandidates(state).Any())
                    { hud.Paragraph("No legal replacement exists at the final four, so the veto cannot be used."); return true; }
                    foreach (var nominee in state.nominees)
                    {
                        string saved = nominee;
                        if (state.hohId == state.playerId) VetoReplacements(state, saved, privateRoom);
                        else hud.ActionFor(saved, "Save " + state.Find(saved).name + " (HoH chooses replacement)", () =>
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
            if (state.phase == EpisodePhase.Eviction && !state.evictionResolved
                && (state.evictionStage == EvictionStage.Voting || state.evictionStage == EvictionStage.Tiebreaker)
                && !state.votes.Any(vote => vote.voterId == state.playerId)
                && (EpisodeEngine.Voters(state).Any(voter => voter.isPlayer) || EpisodeEngine.NeedsPlayerTieBreak(state)))
            {
                bool tieBreak = EpisodeEngine.NeedsPlayerTieBreak(state);
                hud.Paragraph(tieBreak ? "The vote is tied. As HoH, you cast the deciding vote." : "Your ballot is private until the eviction reveal.");
                // At four there is exactly one eligible voter. That has always been true by
                // arithmetic and has never been said, which makes a sole ballot look like a bug.
                if (!tieBreak && EpisodeEngine.Voters(state).Count() == 1)
                    hud.Paragraph("At the final four only one houseguest votes, and tonight that is you. "
                        + "Your single vote decides the eviction outright.");
                VoterRoster(state);
                foreach (var nominee in state.nominees)
                {
                    string id = nominee;
                    hud.ActionFor(id, "Vote to evict " + state.Find(id).name, () => OfferPlayerDecision(state, privateRoom,
                        EpisodeCommandKind.CastVote, (tieBreak ? "Cast the deciding vote to evict " : "Vote privately to evict ")
                        + state.Find(id).name + ". This records your ballot only; the eviction reveal happens at the episode screen.", id));
                }
                return true;
            }
            return false;
        }

        private void VetoReplacements(EpisodeState state, string saved, bool privateRoom)
        {
            hud.Heading("Save " + state.Find(saved).name + " and nominate:");
            foreach (var candidate in EpisodeEngine.ReplacementCandidates(state))
            {
                string id = candidate.id;
                hud.ActionFor(id, candidate.name, () => OfferPlayerDecision(state, privateRoom, EpisodeCommandKind.ResolveVeto,
                    "Use the veto to save " + state.Find(saved).name + " and nominate " + state.Find(id).name + " as the replacement.", saved, id, true));
            }
        }
    }
}
