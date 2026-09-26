using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Gamesim.House;
using Gamesim.Presentation;
using Gamesim.Simulation;
using UnityEngine;

namespace Gamesim.Episode
{
    /// <summary>The opening beats: the premiere, the walk-in, the tour and the introductions before week one.</summary>
    public sealed partial class EpisodeDirector
    {
        /// <summary>
        /// The walk-in's crane, the reference build's three keys: inside the house looking out at the
        /// yard door, up over the bedrooms, and over the top of the whole house, held. The first is
        /// turned fifteen degrees off the doorway so its boom clears the bedroom wall.
        /// </summary>
        public static readonly HouseCameraRig.Shot[] WalkInKeys =
        {
            new HouseCameraRig.Shot { Focus = new Vector3(0f, 1.2f, 10f), Distance = 5f, Pitch = 10f, Yaw = 15f, FieldOfView = 45f, Seconds = 0.01f },
            new HouseCameraRig.Shot { Focus = new Vector3(-1.5f, 1f, 5f), Distance = 11f, Pitch = 38f, Yaw = 15f, FieldOfView = 45f, Seconds = 3f },
            new HouseCameraRig.Shot { Focus = new Vector3(0.075f, 0f, 0f), Distance = 32f, Pitch = 62f, Yaw = 0f, FieldOfView = 45f, Seconds = 7f },
        };

        /// <summary>How long a houseguest reacts to being introduced to: long enough to read, short enough not to hold a pose.</summary>
        private const float IntroductionReaction = 1.6f;

        private bool openingFramedGuest;

        /// <summary>Whether the opening has the house: panels, shortcuts and the camera are its until it ends.</summary>
        private bool OpeningOwnsHouse => opening != null && opening.IsPlaying;

        /// <summary>
        /// Starts the first-run tour, once, for a player who has never seen it.
        ///
        /// <para>Never in batchmode. The tour is the only overlay that waits for a click, so it is
        /// the only one that could hold up an automated season — and a headless run is by definition
        /// not a first-time player. Tests drive <c>HouseTutorial.Show</c> directly instead.</para>
        /// </summary>
        public void OfferTutorial()
        {
            if (tutorial == null || Application.isBatchMode || tutorial.HasSeen) return;
            tutorial.Show(FindChrome);
        }

        /// <summary>
        /// Plays whichever opening beats this season has not seen.
        ///
        /// <para>Never in batchmode, for the reason the tour was never offered there: a sequence that
        /// waits is the only thing that can hold up an automated season, and a headless run is by
        /// definition not seeing any of this. Tests and the standalone verification use
        /// <see cref="PlayOpeningForVerification"/>.</para>
        ///
        /// <para>Only on the first night. A season imported or migrated from before the opening
        /// existed arrives having seen none of it, possibly weeks in; playing the premiere there - and
        /// offering introductions to a house that has already voted someone out - would be wrong on
        /// both counts. It still gets the tour, once.</para>
        /// </summary>
        public void PlayOpening() => StartOpening(stage: true, holdUntilAdvanced: false, verification: false);

        /// <summary>
        /// The opening for a verification run or a test: plays in batchmode too, with the front door
        /// staged only when asked, and with every card held until <see cref="OpeningSequence.Advance"/>
        /// when asked, so a frame can be photographed.
        /// </summary>
        public void PlayOpeningForVerification(bool holdUntilAdvanced = false, bool stage = false, bool holdHeadless = false) =>
            StartOpening(stage, holdUntilAdvanced, verification: true, holdHeadless);

        /// <summary>Whether the opening's front door is up with the house placed behind it. A read, for tests.</summary>
        public bool IsOpeningStaged => openingStage != null && openingStage.Active;

        /// <summary>The on-screen skip, for callers that are not the control itself: stops at the introductions when there are any.</summary>
        public void SkipOpening() => opening?.PressSkip();

        /// <summary>The opening sequence, for tests and the verification tools.</summary>
        public OpeningSequence Opening => opening;

        private void StartOpening(bool stage, bool holdUntilAdvanced, bool verification, bool holdHeadless = false)
        {
            if (opening == null || (Application.isBatchMode && !verification)) { OfferTutorial(); return; }
            if (opening.IsPlaying) return;
            if (!EpisodeEngine.IsFirstNight(projected)) { OfferTutorial(); return; }

            // The house stands still while the show plays: whole seconds banked now, and nothing
            // ticks until it ends. Watching the opening and skipping it leave the house in the same
            // place.
            PauseNpcSocialForPanel();
            openingFramedGuest = false;
            var plan = OpeningPlan(stage, holdUntilAdvanced, verification);
            plan.HoldHeadless = holdHeadless;
            opening.Play(projected.openingBeatsSeen, plan);
            if (!opening.IsPlaying) return;
            SetPlatesSuppressed(true);
            // What the beat that started asked for: a season resumed at the tour starts on the tour,
            // which needs the HUD it points at.
            hud?.SetCinematic(opening.CurrentBeat != OpeningBeat.Tutorial);
            Project();
            // The theme from the first frame of the titles, not from the first commit after them.
            ApplyMusic();
        }

        /// <summary>What the opening needs from the house, for this season as it stands.</summary>
        public OpeningSequence.Settings OpeningPlan(bool stage, bool holdUntilAdvanced = false, bool verification = false)
        {
            var state = projected;
            openingStage = stage ? CreateOpeningStage(verification) : null;
            return new OpeningSequence.Settings
            {
                MarkBeat = MarkOpeningBeat,
                Rig = cameraRig,
                WalkInKeys = WalkInKeys,
                Stage = openingStage,
                RunTutorial = done =>
                {
                    OfferTutorial();
                    if (tutorial == null || !tutorial.IsShowing) { done(); return; }
                    StartCoroutine(WaitForTutorial(done));
                },
                CancelTutorial = () => { if (tutorial != null && tutorial.IsShowing) tutorial.Skip(); },
                Finished = OpeningFinished,
                BeatStarted = OpeningBeatStarted,
                ReducedMotion = reducedMotion,
                HoldUntilAdvanced = holdUntilAdvanced,
                MotionInBatchmode = verification && stage,
                Cast = state.Active.ToList(),
                Seed = unchecked((int)state.seed),
                ArrivalLine = state.events
                    .Where(entry => entry.kind == "arrival")
                    .Select(entry => entry.text)
                    .LastOrDefault(),
                Introduce = IntroduceYourself,
                Introduced = id => projected != null && EpisodeEngine.HasIntroduced(projected, id),
                FrameGuest = FrameForIntroduction,
                GuestReacts = ReactToIntroduction,
            };
        }

        private IEnumerator WaitForTutorial(Action done)
        {
            while (tutorial != null && tutorial.IsShowing) yield return null;
            done();
        }

        private void OpeningBeatStarted(string beat)
        {
            ApplyMusic();
            // The HUD steps aside for the show and comes back for the tour, which points at it -
            // drawn, not pressed: a panel opened from it would run under the opening.
            hud?.SetCinematic(beat != OpeningBeat.Tutorial);
        }

        /// <summary>
        /// The house back to the player, however the opening ended: the front door gone and anybody
        /// left scattered put back, the HUD and the name plates up, faces free to turn, and the
        /// camera on the player.
        /// </summary>
        private void OpeningFinished()
        {
            var stage = openingStage;
            openingStage = null;
            stage?.End(restoreHome: opening != null && opening.WasSkipped);
            hud?.SetCinematic(false, interactive: true);
            SetPlatesSuppressed(false);
            if (introductionLight != null) { Destroy(introductionLight.gameObject); introductionLight = null; }
            // Faces free to turn and heads let go: an introduction holds a head on the lens for
            // half a minute otherwise, through the next cards and into free time.
            if (housemates != null)
                foreach (var npc in housemates)
                {
                    var visual = npc != null ? npc.GetComponent<CharacterPresentation>() : null;
                    if (visual != null) { visual.SetFacing(float.NaN); visual.LookAt(null, 0f); }
                }
            var own = player != null ? player.GetComponentInChildren<CharacterPresentation>() : null;
            if (own != null) { own.SetFacing(float.NaN); own.LookAt(null, 0f); }
            lastIntroduced = null;
            if (cameraRig != null && player != null) cameraRig.FocusSubject(player.transform, reframe: false);
            Project();
            Render();
        }

        /// <summary>
        /// Escape or Start while the opening plays: closes the tour when it is up, puts the keyboard on
        /// "Skip Introductions" during them, and otherwise skips the show - never the introductions.
        /// </summary>
        private void OpeningMenuPressed()
        {
            if (tutorial != null && tutorial.IsShowing) { tutorial.Skip(); return; }
            if (opening.IsMeeting) { opening.FocusSkipIntroductions(); return; }
            opening.PressSkip();
        }

        /// <summary>
        /// Records a finished beat, through the engine like any other decision.
        ///
        /// <para>It is a command rather than a field the presentation writes because the record has
        /// to survive a reload, and the only thing here that survives a reload is the season. It
        /// moves nobody: first impressions come from the introductions themselves.</para>
        /// </summary>
        private void MarkOpeningBeat(string beat)
        {
            if (string.IsNullOrEmpty(beat) || projected.openingBeatsSeen.Contains(beat)) return;
            Commit(projected, EpisodeCommandKind.MarkOpeningBeat, target: beat);
        }

        /// <summary>
        /// The player introducing themselves to a houseguest, committed as its own command: it costs
        /// no interaction from the week's social window and draws nothing from the season's
        /// generator, so how the introductions went can change how the house feels about the player
        /// but never who wins a competition or a vote.
        /// </summary>
        public OpeningSequence.Introduction IntroduceYourself(string npcId, string approachId)
        {
            var origin = projected;
            var result = Submit(new EpisodeCommand
            {
                id = Guid.NewGuid().ToString("N"), actorId = origin.playerId, expectedPhase = origin.phase,
                expectedRevision = origin.revision, kind = EpisodeCommandKind.Introduce, targetId = npcId, secondTargetId = approachId,
            });
            if (!result.accepted) return new OpeningSequence.Introduction { Accepted = false, Reason = result.reason };
            var approach = WebIntroductions.Find(approachId);
            var target = projected.Find(npcId);
            return new OpeningSequence.Introduction
            {
                Accepted = true,
                Outcome = approach != null && target != null ? WebIntroductions.Judge(approach, target.traits) : WebIntroductions.Outcome.Neutral,
            };
        }

        /// <summary>
        /// A houseguest framed for their introduction where they stand: a close shot at their head
        /// height from the way they already face - so they turn to nobody, and the camera stands in
        /// the room they were looking into rather than behind them against whatever the house has
        /// there - a little off-centre so the card on the left leaves them clear, with a soft key
        /// light on them. Framed all from one side, one houseguest had a podium's lamp for a halo
        /// and the rest stood unlit in dim rooms.
        /// </summary>
        private void FrameForIntroduction(string id)
        {
            var body = BodyFor(id);
            if (body == null || cameraRig == null) return;
            // The last houseguest's head is let go as the next is framed.
            var previous = lastIntroduced != null ? lastIntroduced.GetComponent<CharacterPresentation>() : null;
            if (previous != null) previous.LookAt(null, 0f);
            lastIntroduced = body;
            float yaw = IntroductionYaw(body);
            var turn = Quaternion.Euler(0f, yaw, 0f);
            var head = body.position + Vector3.up * 1.35f;
            var shot = new HouseCameraRig.Shot
            {
                Focus = head + turn * Vector3.right * -0.6f,
                Distance = 3.4f, Pitch = 12f, Yaw = yaw, FieldOfView = 40f,
                Seconds = openingFramedGuest ? 0.6f : 0.8f, DepthOfFieldWeight = 1f,
            };
            cameraRig.MoveTo(shot);
            openingFramedGuest = true;
            KeyLight(head, Quaternion.Euler(shot.Pitch, yaw, 0f), shot.Focus, shot.Distance);
            var visual = body.GetComponent<CharacterPresentation>();
            if (visual == null) return;
            visual.SetFacing(Mathf.Repeat(yaw + 180f, 360f));
            if (cameraRig.ViewCamera != null) visual.LookAt(cameraRig.ViewCamera.transform, 30f);
        }

        /// <summary>
        /// Which way to look at a houseguest for their introduction. Looking in towards the middle
        /// of the house comes first, so the room is behind them rather than the house's edge - near
        /// the middle, the way their place was authored to face instead - and then the directions
        /// around it, taking the one where the fewest small things stand between the lens and their
        /// face or just behind their head. Framed from one side every time, the houseguest standing on the
        /// competition course had a station lamp for a face from one side and a halo from the other.
        /// Nothing here depends on how they walked home, so the shot is the same watched or skipped.
        /// </summary>
        private float IntroductionYaw(Transform body)
        {
            var inward = cameraRig.HouseCenter - body.position;
            inward.y = 0f;
            float facing = body.eulerAngles.y;
            if (housemates != null && initialNpcRotations != null)
                for (int i = 0; i < housemates.Length && i < initialNpcRotations.Length; i++)
                    if (housemates[i] != null && housemates[i].transform == body) facing = initialNpcRotations[i].eulerAngles.y;
            float preferred = inward.sqrMagnitude > 4f ? Mathf.Atan2(inward.x, inward.z) * Mathf.Rad2Deg : facing + 180f;

            var head = body.position + Vector3.up * 1.5f;
            // The props that can spoil a face: small things near them - lamps, poles, plants, stacks -
            // and not the floors, walls and furniture runs a room is made of, nor the houseguests.
            var props = FindObjectsByType<Renderer>(FindObjectsSortMode.None)
                .Where(renderer => renderer.enabled && renderer.gameObject.activeInHierarchy
                    && (renderer.bounds.center - head).sqrMagnitude < 36f
                    && Mathf.Max(renderer.bounds.size.x, renderer.bounds.size.z) < 1.6f
                    && renderer.bounds.max.y > 0.9f
                    && renderer.GetComponentInParent<HouseNpc>() == null && renderer.GetComponentInParent<HousePlayerController>() == null
                    && renderer.GetComponentInParent<Canvas>() == null)
                .Select(renderer => { var box = renderer.bounds; box.Expand(0.12f); return box; })
                // A prop the head is already inside spoils every direction alike, so it chooses none.
                .Where(box => !box.Contains(head))
                .ToList();
            // The least spoiled of eight directions: a prop across the face counts ten times one
            // behind the head, and a tie goes to the direction nearer the preferred one.
            float best = Mathf.Repeat(preferred, 360f);
            int bestScore = int.MaxValue;
            foreach (float turn in new[] { 0f, 45f, -45f, 90f, -90f, 135f, -135f, 180f })
            {
                float yaw = Mathf.Repeat(preferred + turn, 360f);
                var look = Quaternion.Euler(12f, yaw, 0f) * Vector3.forward;
                var eye = head - look * 3.4f;
                var behind = head + new Vector3(look.x, 0f, look.z).normalized * 1.5f;
                int score = props.Count(box => Crosses(box, eye, head)) * 10 + props.Count(box => Crosses(box, head, behind));
                if (score < bestScore) { bestScore = score; best = yaw; }
            }
            return best;
        }

        private static bool Crosses(Bounds box, Vector3 from, Vector3 to)
        {
            var direction = to - from;
            float length = direction.magnitude;
            return length > 1e-4f && box.IntersectRay(new Ray(from, direction / length), out float distance) && distance <= length;
        }

        private Light introductionLight;
        private Transform lastIntroduced;

        /// <summary>A soft spot from beside and above the camera onto the face being introduced; gone when the opening ends.</summary>
        private void KeyLight(Vector3 head, Quaternion view, Vector3 focus, float distance)
        {
            if (introductionLight == null)
            {
                var lamp = new GameObject("Introduction key light");
                lamp.transform.SetParent(transform, false);
                introductionLight = lamp.AddComponent<Light>();
                introductionLight.type = LightType.Spot;
                introductionLight.color = new Color(1f, 0.95f, 0.88f);
                introductionLight.intensity = 6f;
                introductionLight.range = 9f;
                introductionLight.spotAngle = 34f;
                introductionLight.innerSpotAngle = 18f;
                introductionLight.shadows = LightShadows.None;
            }
            var eye = focus - view * Vector3.forward * distance;
            var place = eye + Vector3.up * 0.7f + view * Vector3.right * 0.9f;
            introductionLight.transform.position = place;
            introductionLight.transform.rotation = Quaternion.LookRotation(head - place);
            introductionLight.enabled = true;
        }

        /// <summary>
        /// How a houseguest takes the player's introduction, in the body: a cheer for one that
        /// landed, a word for a polite one, a flash of temper for one that clashed. The card says the
        /// rest; the reference build's meet had no bodies to do this with.
        /// </summary>
        private void ReactToIntroduction(string id, WebIntroductions.Outcome outcome)
        {
            if (outcome == WebIntroductions.Outcome.Match) { React(id, CharacterPresentation.Reaction.Cheered); return; }
            var body = BodyFor(id);
            var visual = body != null ? body.GetComponent<CharacterPresentation>() : null;
            if (visual != null) StartCoroutine(ReactFor(visual, outcome == WebIntroductions.Outcome.Clash));
        }

        private static IEnumerator ReactFor(CharacterPresentation visual, bool tense)
        {
            if (tense) visual.SetArguing(true); else visual.SetTalking(true);
            float waited = 0f;
            while (waited < IntroductionReaction && visual != null) { waited += Time.unscaledDeltaTime; yield return null; }
            if (visual == null) yield break;
            if (tense) visual.SetArguing(false); else visual.SetTalking(false);
        }

        private void SetPlatesSuppressed(bool suppressed)
        {
            if (housemates == null) return;
            foreach (var npc in housemates)
                if (npc != null) npc.PlateSuppressed = suppressed;
        }
    }
}
