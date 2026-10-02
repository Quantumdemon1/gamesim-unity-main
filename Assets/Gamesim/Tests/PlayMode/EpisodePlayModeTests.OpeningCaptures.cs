using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Gamesim.Episode;
using Gamesim.House;
using Gamesim.Presentation;
using Gamesim.Simulation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Gamesim.Tests.PlayMode
{
    /// <summary>
    /// Every beat of the opening photographed for review, headless: the title, the player at the
    /// front door with their name up and the door still shut, the door giving, a houseguest on the
    /// mark, the house together, the house entry, the walk-in's crane, the introductions and the
    /// house handed back - and the same show on cards, as a house that cannot be staged plays it.
    ///
    /// <para>This began as a probe run only on the D: copy, whose frames were the ones the opening
    /// was reviewed by. A probe that photographs whatever happens to be on screen is how the
    /// committed closed-door frame came to show the whole-house overview the camera was still
    /// leaving. So each frame here is taken only once the state it is supposed to show has been
    /// asserted - the beat, whose reveal it is, the lower third fully in, the camera landed where a
    /// shot should have landed - and each is checked afterwards for what it should have drawn: a face
    /// in every portrait and something on every card, not just a frame that is not one flat colour.
    /// </para>
    ///
    /// <para>The PNGs land beside the project, as every framing capture does
    /// (<c>opening-staged-NN-*.png</c>, <c>opening-cards-NN-*.png</c>; on the D: copy,
    /// D:\GamesimAcceptance). The faces are asked for before the show starts, so the frames are
    /// judged on whether a face drew rather than on how quickly the portrait studio builds one.
    /// Batchmode only: the interactive editor would write them into the working project, and its
    /// frames are not headless ones.</para>
    /// </summary>
    public sealed partial class EpisodePlayModeTests
    {
        /// <summary>How long the title card takes to arrive in full: its subtitle, last in, has risen by 3.2 s (IntroSequence.tsx:464-471).</summary>
        private const float TitleArrivedSeconds = 3.3f;

        /// <summary>When the group card is complete: the last face has popped in by 0.8 s and the welcome under it has risen by 1.2 s.</summary>
        private const float GroupArrivedSeconds = 1.3f;

        /// <summary>When the house entry is complete: its card is in by 0.9 s and the season's arrival line, in the card, by 2.5 s; the card holds for the beat.</summary>
        private const float HouseEntryArrivedSeconds = 2.6f;

#if GAMESIM_UMA
        // UMA only: these frames are judged on the faces in them, and only UMA builds a face (2026-09-27).
        /// <summary>
        /// The staged opening, beat by beat, as it plays in the house: the front door built in the
        /// west yard, everybody walked through it, the walk-in's crane, and the introductions framed
        /// where each houseguest stands. Continue moves it on where a player would press it, and
        /// never where a press would end a moment before it is photographed.
        /// </summary>
        [UnityTest]
        public IEnumerator OpeningCaptures_EveryStagedBeatIsPhotographedWithContent()
        {
            if (!Application.isBatchMode) yield break;

            var opening = director.Opening;
            var state = director.Snapshot;
            string playerId = state.playerId;
            var cast = CastPlayerFirst(state);
            var guests = cast.Where(person => !person.isPlayer).ToList();
            Assert.That(guests, Has.Count.GreaterThanOrEqualTo(2), "The fixture house has two houseguests to meet at least.");
            // Asked for now, so the studio builds them while the house loads and the title holds.
            foreach (var person in cast) CharacterPortraits.Get(person);

            var watcher = new GameObject("Opening capture watcher").AddComponent<RevealWatcher>();
            watcher.Sequence = opening;
            director.PlayOpeningForVerification(holdUntilAdvanced: true, stage: true, armSeconds: 0f);
            Assert.That(opening.IsPlaying, Is.True, "The opening plays on the first night.");
            yield return WaitFor(() => director.IsOpeningStaged, 40f, "The house is placed behind the front door once every body is built.");

            // 01 - the title card, in full. It holds until it is moved on.
            yield return WaitFor(() => SequenceNode(opening, "Title card") != null, 5f, "The title card comes up once the house is placed.");
            float titleAt = Time.realtimeSinceStartup;
            yield return WarmOpeningPortraits(cast, 20f);
            yield return RealSeconds(titleAt + TitleArrivedSeconds - Time.realtimeSinceStartup);
            var title = SequenceNode(opening, "Title card");
            Assert.That(opening.CurrentBeat, Is.EqualTo(OpeningBeat.Intro));
            Assert.That(opening.CurrentGuestId, Is.Null, "Nobody is being introduced under the title.");
            Assert.That(title, Is.Not.Null, "The title is still up: it holds until it is moved on.");
            Assert.That(title.GetComponent<CanvasGroup>().alpha, Is.GreaterThanOrEqualTo(0.99f), "The title card has faded in.");
            yield return CaptureFraming("opening-staged-01-title", settle: false, inspect: frame =>
                AssertRegionHasContent(frame, CaptureRectOf(title, 0.6f, 0.5f), "The middle of the title card, where the wordmark stands"));
            opening.Advance();

            // 02 - the player at the front door, named, with the door still shut.
            yield return WaitFor(() => opening.CurrentGuestId == playerId, 20f, "The player is revealed first, through the front door.");
            var set = Object.FindFirstObjectByType<OpeningDoorSet>();
            Assert.That(set, Is.Not.Null, "The front door is standing in the yard.");
            watcher.Door = set;
            yield return WaitForTheClosedDoorFrame(opening, set, playerId);
            var playerThird = SequenceNode(opening, "Lower third");
            Assert.That(SequenceTexts(playerThird, "Name").Single(), Does.Contain(cast[0].name), "The lower third names the player.");
            Assert.That(SequenceTexts(playerThird, "Counter").Single(), Is.EqualTo("1 of " + cast.Count), "and counts them first of the house.");
            // The door is read from the capture's inspection, while the photographed frame is still
            // current: it gives on its own clock, which nothing holds, and the capture's work is one
            // long frame to that clock, so read after the capture returns it could have opened on a
            // frame that was photographed shut.
            bool shutWhenTaken = false;
            yield return CaptureFraming("opening-staged-02-door-closed", settle: false, inspect: frame =>
            {
                shutWhenTaken = !set.IsOpen && set.Openness < 0.01f;
                AssertFaceIsDrawn(frame, playerThird.Find("Portrait"), "The player's face in the lower third over the closed door");
            });
            Assert.That(shutWhenTaken, Is.True, "The door was still shut, its leaves unmoved, when the frame was taken.");
            // The count on its chip, in paper: dim grey on the dark yard, it was all but invisible
            // over the shut door (UI-UX-PASS-PLAN S0, sweep-show 21).
            var playerCount = SequenceLabels(playerThird).Single(label => label.name == "Counter");
            AssertOnAGround(playerCount.transform, "The reveal's count over the shut door");
            Assert.That(playerCount.color, Is.EqualTo(UiTheme.Paper), "The count is paper on its chip, not muted on the yard.");

            // 03 - the door gives and the camera pushes in, the name still up.
            yield return WaitFor(() => set.IsOpen && set.Openness > 0.6f, 8f, "The door opens for the player and swings wide.");
            Assert.That(opening.CurrentGuestId, Is.EqualTo(playerId));
            Assert.That(cameraRig.HasShot, Is.True);
            Assert.That(cameraRig.DesiredDistance, Is.EqualTo(PushInDistanceForTests).Within(0.1f), "The camera pushes in as the door opens.");
            Assert.That(LowerThirdHasArrived(opening), Is.True, "The name stays up while the door opens.");
            var openThird = SequenceNode(opening, "Lower third");
            // The Continue hint on its ground on the skip pill's row, out of the lit doorway
            // (UI-UX-PASS-PLAN S0, sweep-show 5), and the count on its chip, faded all the way in.
            var openHint = SequenceNode(opening, "Hint");
            var openChip = SequenceNode(opening, "Counter chip");
            var openPill = SequenceButtons(opening, OpeningSequence.SkipCaption).Single(button => button.IsActive()).transform;
            AssertOnAGround(openHint.Find("Label"), "The Continue hint as the door opens", shown: true);
            AssertOnAGround(SequenceLabels(openChip).Single(label => label.name == "Counter").transform, "The reveal's count as the door opens", shown: true);
            Assert.That(openChip.GetComponent<CanvasGroup>().alpha, Is.GreaterThanOrEqualTo(0.99f), "The count's chip has faded in.");
            yield return CaptureFraming("opening-staged-03-door-open", settle: false, inspect: frame =>
            {
                AssertFaceIsDrawn(frame, openThird.Find("Portrait"), "The player's face in the lower third as the door opens");
                AssertGroundIsDrawn(frame, openHint.Find(OpeningSequence.GroundName), openPill, "The Continue hint's ground on the skip pill's row");
                AssertGroundIsDrawn(frame, openChip.Find(OpeningSequence.GroundName), openPill, "The reveal's count's chip");
            });

            // 04 - the first houseguest after the player, on the mark in front of the lens. One press
            // once the half-beat before their walk is over opens the door as soon as they reach it
            // rather than at the reveal's own time; a later one would end their moment on the mark
            // before it could be photographed.
            var first = guests[0];
            yield return SequenceStep(opening, () => opening.CurrentGuestId != playerId, 12f, int.MaxValue);
            Assert.That(opening.CurrentGuestId, Is.EqualTo(first.id), "The house follows the player in the season's order.");
            yield return RealSeconds(0.4f);
            opening.Advance();
            var body = SceneComponents<HouseNpc>().Single(npc => npc.Id == first.id).transform;
            yield return WaitFor(() => set.IsOpen, 8f, "The door opens for " + first.name + ".");
            yield return WaitFor(() => Flat(body.position, RevealMarkForTests) < 0.6f
                                       && Mathf.Abs(Mathf.DeltaAngle(body.eulerAngles.y, 90f)) < 35f
                                       && LowerThirdHasArrived(opening)
                                       && cameraRig.HasArrived(0.1f) && !cameraRig.IsTravelling, 10f,
                first.name + " walks through the door to the mark and turns to the lens, named, with the camera settled.");
            Assert.That(opening.CurrentGuestId, Is.EqualTo(first.id), "It is still their moment: the mark holds until it is moved on.");
            Assert.That(cameraRig.HasShot, Is.True);
            Assert.That(cameraRig.DesiredDistance, Is.EqualTo(PushInDistanceForTests).Within(0.1f), "The camera holds the push-in on the mark.");
            var guestThird = SequenceNode(opening, "Lower third");
            Assert.That(SequenceTexts(guestThird, "Name").Single(), Does.Contain(first.name), "The lower third names them.");
            Assert.That(SequenceTexts(guestThird, "Counter").Single(), Is.EqualTo("2 of " + cast.Count));
            yield return CaptureFraming("opening-staged-04-guest-on-mark", settle: false, inspect: frame =>
                AssertFaceIsDrawn(frame, guestThird.Find("Portrait"), first.name + "'s face in the lower third on the mark"));

            // The rest of the house, moved along the way a player tapping Continue moves it: each door
            // opens as its houseguest reaches it, and each moment on the mark ends as they arrive.
            // Continue stops once the last houseguest is up, and their reveal plays out: a press made
            // every frame leaves one pending as the last reveal ends, which the group card's own hold
            // takes - the card went straight into the fade to black and was photographed black.
            var last = cast[cast.Count - 1].id;
            yield return SequenceStep(opening, () => opening.CurrentGuestId == last, 12f * guests.Count, int.MaxValue);
            Assert.That(opening.CurrentGuestId, Is.EqualTo(last), "The reveals reach the last houseguest.");
            // Holds wait for Continue in this run, so the last moment on the mark is ended with one
            // press made once they are there, which that hold takes.
            var lastBody = SceneComponents<HouseNpc>().Single(npc => npc.Id == last).transform;
            yield return WaitFor(() => Flat(lastBody.position, RevealMarkForTests) < 0.6f, 20f, "The last houseguest reaches the mark.");
            opening.Advance();
            yield return WaitFor(() => opening.CurrentGuestId == null, 10f, "The last houseguest's reveal plays out.");
            Assert.That(opening.CurrentGuestId, Is.Null, "Every houseguest has been revealed.");
            CollectionAssert.AreEqual(cast.Select(person => person.id), watcher.Revealed,
                "Every houseguest came through the front door once: the player first, then the house in the season's order.");
            Assert.That(watcher.DoorOpenings, Is.EqualTo(cast.Count), "The door opened for each of them.");

            // 05 - the house together on one card, with the confetti thrown over it.
            yield return WaitFor(() => SequenceNode(opening, "Group") != null, 5f, "The house comes together on one card.");
            float groupAt = Time.realtimeSinceStartup;
            var group = SequenceNode(opening, "Group");
            Assert.That(SequenceNode(opening, "Confetti"), Is.Not.Null, "Confetti is thrown over the house together: this run plays the movement.");
            var groupFaces = group.Cast<Transform>().Where(child => child.name == "Portrait").ToList();
            Assert.That(groupFaces, Has.Count.EqualTo(cast.Count), "Everybody is on it.");
            yield return WaitFor(() => groupFaces.All(face => face != null && face.localScale.x >= 0.99f), 3f, "Every face has popped in.");
            yield return RealSeconds(groupAt + GroupArrivedSeconds - Time.realtimeSinceStartup);
            Assert.That(opening.CurrentBeat, Is.EqualTo(OpeningBeat.Intro));
            Assert.That(opening.CurrentGuestId, Is.Null);
            Assert.That(SequenceNode(opening, "Group"), Is.SameAs(group), "The card holds until it is moved on.");
            Assert.That(GameObject.Find(OpeningDoorSet.RootName), Is.Null, "The front door came down behind the card.");
            Assert.That(opening.MusicClosing, Is.False, "The fade to black has not begun: the card is what is on screen.");
            yield return CaptureFraming("opening-staged-05-group", settle: false, inspect: frame =>
            {
                foreach (var face in groupFaces) AssertFaceIsDrawn(frame, face, "A face on the group card");
            });

            // 06 - the house entry, its arrival line in.
            yield return SequenceStep(opening, () => opening.CurrentBeat == OpeningBeat.HouseEntry, 8f, int.MaxValue);
            Assert.That(opening.CurrentBeat, Is.EqualTo(OpeningBeat.HouseEntry), "The group card and the fade to black give way to the house entry.");
            Assert.That(director.Snapshot.openingBeatsSeen, Does.Contain(OpeningBeat.Intro), "The intro was recorded.");
            float entryAt = Time.realtimeSinceStartup;
            yield return WaitFor(() => FadedIn(SequenceNode(opening, "Card")), 3f, "The house-entry card grows in.");
            yield return RealSeconds(entryAt + HouseEntryArrivedSeconds - Time.realtimeSinceStartup);
            var entryCard = SequenceNode(opening, "Card");
            Assert.That(opening.CurrentBeat, Is.EqualTo(OpeningBeat.HouseEntry), "The house entry holds until it is moved on.");
            Assert.That(FadedIn(entryCard), Is.True, "Its card is still up.");
            // The season's line in the card, in the house's word (UI-UX-PASS-PLAN S0, sweep-show 22):
            // low on the screen it ran edge to edge over the Continue hint, and said "housemates".
            var entryLine = SequenceLabels(entryCard).Single(label => label.name == "Line");
            Assert.That(entryLine.text, Does.Not.Contain("housemates").IgnoreCase, "The line says houseguests, as every other card does: " + entryLine.text);
            Assert.That(Inside((RectTransform)entryCard, entryLine.rectTransform), Is.True, "and stands in the card,");
            Assert.That(entryLine.alpha, Is.GreaterThanOrEqualTo(0.79f), "faded in to its 0.8,");
            entryLine.ForceMeshUpdate();
            Assert.That(entryLine.isTextTruncated, Is.False, "every word of it drawn.");
            var entryHint = SequenceNode(opening, "Hint");
            var entryPill = SequenceButtons(opening, OpeningSequence.SkipCaption).Single(button => button.IsActive()).transform;
            Assert.That(SequenceWorldRect((RectTransform)entryHint).Overlaps(SequenceWorldRect(entryLine.rectTransform)), Is.False, "The line no longer crowds the Continue hint.");
            AssertOnAGround(entryHint.Find("Label"), "The Continue hint on the house entry", shown: true);
            yield return CaptureFraming("opening-staged-06-house-entry", settle: false, inspect: frame =>
            {
                AssertRegionHasContent(frame, CaptureRectOf(entryCard), "The house-entry card");
                AssertGroundIsDrawn(frame, entryHint.Find(OpeningSequence.GroundName), entryPill, "The Continue hint's ground on the house entry");
            });
            opening.Advance();

            // 07 to 09 - the walk-in's crane, on its own schedule: the push-in to the doorway, the rise
            // over the bedrooms, and the crane over the top of the house, each frame on its way to the
            // key it should be heading for then and higher than the one before.
            yield return WaitFor(() => opening.CurrentBeat == OpeningBeat.WalkIn, 5f, "The house walks in.");
            float walkInAt = Time.realtimeSinceStartup;
            var keys = EpisodeDirector.WalkInKeys;
            OpeningSequence.WalkInSchedule(keys, out float riseAt, out float craneAt, out float settledAt);
            var moments = new[]
            {
                (Name: "opening-staged-07-walkin-doorway", At: riseAt * 0.6f, Key: OpeningSequence.WalkInDoorway),
                (Name: "opening-staged-08-walkin-rising", At: (riseAt + craneAt) * 0.5f, Key: OpeningSequence.WalkInRise),
                (Name: "opening-staged-09-walkin-crane", At: craneAt + (settledAt - craneAt) * 0.25f, Key: OpeningSequence.WalkInOver),
            };
            float lastHeight = float.MinValue;
            foreach (var moment in moments)
            {
                yield return RealSeconds(walkInAt + moment.At - Time.realtimeSinceStartup);
                string when = moment.At.ToString("0.0") + " s into the walk-in";
                Assert.That(opening.CurrentBeat, Is.EqualTo(OpeningBeat.WalkIn), "The walk-in is still playing " + when + ".");
                Assert.That(cameraRig.HasShot, Is.True, "The crane is the show's shot " + when + ".");
                Assert.That(cameraRig.DesiredDistance, Is.EqualTo(keys[moment.Key].Distance).Within(0.05f),
                    "The camera is on its way to walk-in key " + moment.Key + " " + when + ".");
                float height = cameraRig.ViewCamera.transform.position.y;
                Assert.That(height, Is.GreaterThan(lastHeight), "The camera is higher " + when + " than at the frame before: the crane rises.");
                lastHeight = height;
                yield return CaptureFraming(moment.Name, settle: false);
            }
            // Continue on the walk-in, as a player might: the introductions put anybody not yet home
            // back behind black first.
            opening.Advance();

            // 10 - the first introduction: the card, and the houseguest framed where they stand.
            yield return WaitFor(() => opening.IsMeeting, 10f, "The introductions follow the walk-in.");
            yield return WaitFor(() => MeetCardIsSettled(opening, guests[0].id), 8f,
                "The first houseguest's card is up and the camera has landed on them.");
            var card = SequenceNode(opening, "Card");
            Assert.That(opening.CurrentBeat, Is.EqualTo(OpeningBeat.MeetAndGreet));
            Assert.That(opening.CurrentGuestId, Is.EqualTo(guests[0].id), "The introductions start with the first houseguest in the season's order.");
            Assert.That(SequenceTexts(card, "Name").Single(), Is.EqualTo(guests[0].name), "The card is theirs.");
            Assert.That(SequenceTexts(opening, "Count").Single(), Is.EqualTo("1 of " + guests.Count));
            foreach (var caption in new[] { "Warm", "Calculated", "Bold" })
                Assert.That(SequenceButtons(opening, caption).Count(button => button.IsInteractable()), Is.EqualTo(1), caption + " can be chosen.");
            Assert.That(SequenceNode(opening, "Scrim").GetComponent<Image>().color.a, Is.GreaterThan(0f).And.LessThan(0.9f),
                "The house shows through behind the card.");
            // The header on a ground of its own: it read through the yard's sign (UI-UX-PASS-PLAN S0, sweep-show 24).
            var column = SequenceNode(opening, "Introductions");
            AssertOnAGround(SequenceLabels(column).Single(label => label.name == "Heading").transform, "The introductions' heading", shown: true);
            AssertOnAGround(SequenceLabels(column).Single(label => label.name == "Count").transform, "The introductions' count", shown: true);
            var headerGround = column.Find(OpeningSequence.GroundName);
            // The last choice, which the keyboard is not on: the same raised fill, at rest.
            var restingRow = SequenceButtons(opening, "Bold").Single().transform;
            yield return CaptureFraming("opening-staged-10-meet-card", settle: true, inspect: frame =>
            {
                AssertFaceIsDrawn(frame, card.Find("Portrait"), guests[0].name + "'s face on their card");
                AssertGroundIsDrawn(frame, headerGround, restingRow, "The introductions' header's ground");
            });

            // 11 - the player introduces themselves, and the houseguest answers in words and in the body.
            var guestBody = SceneComponents<HouseNpc>().Single(npc => npc.Id == guests[0].id);
            SequenceButtons(opening, "Calculated").Single().onClick.Invoke();
            yield return WaitFor(() => FadedIn(SequenceNode(opening, "Answer")), 3f, "The answer comes up where the question was.");
            // Into the houseguest's reaction, which lasts 1.6 s.
            yield return RealSeconds(0.5f);
            var answer = SequenceNode(opening, "Answer");
            Assert.That(EpisodeEngine.HasIntroduced(director.Snapshot, guests[0].id), Is.True, "The introduction was committed through the engine.");
            Assert.That(SequenceTexts(answer, "Reaction").Single(), Does.StartWith(guests[0].name + ":"), "They answer, by name.");
            Assert.That(SequenceButtons(opening, "Calculated"), Is.Empty, "The choices go once one is made.");
            Assert.That(SequenceButtons(opening, OpeningSequence.NextCaption).Count(button => button.IsInteractable()), Is.EqualTo(1),
                "Next stands under the answer.");
            yield return CaptureFraming("opening-staged-11-meet-reaction", settle: false, inspect: frame =>
            {
                AssertRegionHasContent(frame, CaptureRectOf(answer), "The answer and its Next");
                // The speaker's body in the reaction's frame: it had walked out of it, and the shot
                // held on an empty corner and a lamp (UI-UX-PASS-PLAN S0, sweep-show 24). The hips,
                // where the body is drawn, projected through the camera the frame was drawn with.
                var hips = HipsOf(guestBody);
                var at = hips != null ? hips.position : guestBody.transform.position + Vector3.up * 0.95f;
                var seen = InTheFrame(cameraRig.ViewCamera, at);
                // For the record of the drift's cause on a UMA body: how far the drawn body stands
                // from its root on the reaction's frame, and where the root stands.
                Debug.Log("[Gamesim] reaction frame: " + guests[0].name + "'s hips at " + at.ToString("F2") + ", root at "
                    + guestBody.transform.position.ToString("F2") + " (" + Flat(at, guestBody.transform.position).ToString("F2") + " m apart), in the frame at "
                    + (seen.HasValue ? seen.Value.ToString("F2") : "behind the lens") + ".");
                Assert.That(InsideTheFrame(seen), Is.True, guests[0].name + "'s body is in the reaction's frame: hips at " + at.ToString("F2")
                    + " -> " + (seen.HasValue ? seen.Value.ToString("F2") : "behind the lens") + ".");
            });

            // 12 - Next: the card leaves, the next comes in, and the camera goes to the next houseguest.
            SequenceButtons(opening, OpeningSequence.NextCaption).Single().onClick.Invoke();
            yield return WaitFor(() => MeetCardIsSettled(opening, guests[1].id), 8f,
                "The second houseguest's card is up and the camera has landed on them.");
            var secondCard = SequenceNode(opening, "Card");
            Assert.That(secondCard, Is.Not.SameAs(card), "It is a new card.");
            Assert.That(SequenceTexts(secondCard, "Name").Single(), Is.EqualTo(guests[1].name), "and theirs.");
            Assert.That(SequenceTexts(opening, "Count").Single(), Is.EqualTo("2 of " + guests.Count));
            yield return CaptureFraming("opening-staged-12-meet-card-2", settle: true, inspect: frame =>
                AssertFaceIsDrawn(frame, secondCard.Find("Portrait"), guests[1].name + "'s face on their card"));

            // 13 - the rest of the introductions skipped, and the house handed back.
            opening.SkipIntroductions();
            yield return WaitFor(() => !opening.IsPlaying, 5f, "Skipping the introductions ends the opening.");
            CollectionAssert.AreEqual(OpeningBeat.InOrder, director.Snapshot.openingBeatsSeen, "Every beat was recorded, in order.");
            Assert.That(director.IsOpeningStaged, Is.False);
            Assert.That(GameObject.Find(OpeningDoorSet.RootName), Is.Null, "The front door is gone.");
            Assert.That(director.IsPanelOpen, Is.False, "The house is the player's again.");
            Assert.That(cameraRig.ControlsEnabled, Is.True, "and so is the camera,");
            Assert.That(cameraRig.HasShot, Is.False, "which has let the introductions' shot go.");
            yield return WaitFor(() => !cameraRig.IsTravelling && cameraRig.HasArrived(0.35f), 5f, "The camera settles back on the house.");
            yield return CaptureFraming("opening-staged-13-after", settle: true);
            Object.Destroy(watcher.gameObject);
        }
#endif

#if GAMESIM_UMA
        // UMA only: these frames are judged on the faces in them, and only UMA builds a face (2026-09-27).
        /// <summary>
        /// The opening without the front door - reduced motion, a house that cannot be staged - where
        /// every reveal is a portrait on a card, the reference build's own reveal for a houseguest with
        /// no body. Headless the cards stand still, so each frame is the card as it lands.
        /// </summary>
        [UnityTest]
        public IEnumerator OpeningCaptures_EveryCardBeatIsPhotographedWithContent()
        {
            if (!Application.isBatchMode) yield break;

            var opening = director.Opening;
            var cast = CastPlayerFirst(director.Snapshot);
            foreach (var person in cast) CharacterPortraits.Get(person);
            yield return WarmOpeningPortraits(cast, 20f);

            director.PlayOpeningForVerification(holdUntilAdvanced: true, stage: false, armSeconds: 0f);
            Assert.That(opening.IsPlaying, Is.True, "The opening plays on the first night.");
            Assert.That(director.IsOpeningStaged, Is.False, "No front door is built for the cards.");

            // 01 - the title.
            yield return WaitFor(() => FadedIn(SequenceNode(opening, "Title card")), 5f, "The title card is up.");
            var title = SequenceNode(opening, "Title card");
            Assert.That(opening.CurrentBeat, Is.EqualTo(OpeningBeat.Intro));
            Assert.That(opening.CurrentGuestId, Is.Null, "Nobody is being introduced under the title.");
            yield return CaptureFraming("opening-cards-01-title", settle: false, inspect: frame =>
                AssertRegionHasContent(frame, CaptureRectOf(title, 0.6f, 0.5f), "The middle of the title card, where the wordmark stands"));

            // 02 and 03 - the player's card, then the first houseguest's: the portrait up out of its
            // glow, and the lower third under it.
            for (int i = 0; i < 2; i++)
            {
                var person = cast[i];
                string frameName = "opening-cards-0" + (i + 2) + (i == 0 ? "-reveal" : "-reveal-2");
                yield return SequenceStep(opening, () => opening.CurrentGuestId == person.id, 5f, int.MaxValue);
                Assert.That(opening.CurrentGuestId, Is.EqualTo(person.id), i == 0 ? "The player is revealed first." : "The house follows in the season's order.");
                yield return WaitFor(() => LowerThirdHasArrived(opening), 3f, person.name + "'s name comes up under them.");
                var ground = SequenceNode(opening, "Stage");
                Assert.That(ground, Is.Not.Null);
                var portrait = ground.Cast<Transform>().LastOrDefault(child => child.name == "Portrait" && child.gameObject.activeInHierarchy);
                Assert.That(portrait, Is.Not.Null, person.name + " is revealed as a portrait on a card.");
                var third = SequenceNode(opening, "Lower third");
                Assert.That(SequenceTexts(third, "Name").Single(), Does.Contain(person.name), "The lower third names them.");
                Assert.That(SequenceTexts(third, "Counter").Single(), Is.EqualTo((i + 1) + " of " + cast.Count));
                yield return CaptureFraming(frameName, settle: false, inspect: frame =>
                {
                    AssertFaceIsDrawn(frame, portrait, person.name + "'s portrait on the card");
                    AssertFaceIsDrawn(frame, third.Find("Portrait"), person.name + "'s face in the lower third");
                });
            }

            // 04 - the house together.
            yield return SequenceStep(opening, () => SequenceNode(opening, "Group") != null, 10f, int.MaxValue);
            var group = SequenceNode(opening, "Group");
            Assert.That(group, Is.Not.Null, "The house comes together on one card.");
            Assert.That(opening.CurrentGuestId, Is.Null, "after every houseguest has been revealed.");
            var faces = group.Cast<Transform>().Where(child => child.name == "Portrait").ToList();
            Assert.That(faces, Has.Count.EqualTo(cast.Count), "Everybody is on it.");
            yield return WaitFor(() => faces.All(face => face != null && face.localScale.x >= 0.99f), 3f, "Every face is on it.");
            yield return CaptureFraming("opening-cards-04-group", settle: false, inspect: frame =>
            {
                foreach (var face in faces) AssertFaceIsDrawn(frame, face, "A face on the group card");
            });

            opening.Skip();
            yield return WaitFor(() => !opening.IsPlaying, 3f, "The opening ends when it is skipped.");
        }
#endif

        // ---------------------------------------------------------------- helpers

        /// <summary>The house as the opening introduces it: the player, then everybody else in the season's order.</summary>
        private static List<ContestantState> CastPlayerFirst(EpisodeState state) =>
            state.Active.OrderBy(person => person.isPlayer ? 0 : 1).ToList();

        /// <summary>Whether a piece of the opening is on screen and faded all the way in.</summary>
        private static bool FadedIn(Transform node)
        {
            var fader = node != null ? node.GetComponent<CanvasGroup>() : null;
            return fader != null && fader.alpha >= 0.99f;
        }

        /// <summary>
        /// Whether an introduction is ready to photograph: this houseguest's card fully in with the
        /// last one gone, and the camera landed on them with its depth of field settled.
        /// </summary>
        private bool MeetCardIsSettled(OpeningSequence opening, string guestId) =>
            opening.IsMeeting && opening.CurrentGuestId == guestId
            && FadedIn(SequenceNode(opening, "Card")) && SequenceNode(opening, "Leaving") == null
            && cameraRig.HasShot && cameraRig.HasArrived(0.1f) && !cameraRig.IsTravelling
            && Mathf.Abs(cameraRig.DepthOfFieldWeight - cameraRig.DesiredDepthOfFieldWeight) < 0.01f;

        /// <summary>
        /// Asks for every houseguest's portrait and waits for them all, so that a frame showing a face
        /// is judged on whether the face drew rather than on how fast the studio builds one look at a
        /// time. A face that never arrives fails here, by name.
        /// </summary>
        private static IEnumerator WarmOpeningPortraits(IList<ContestantState> cast, float seconds)
        {
            float until = Time.realtimeSinceStartup + seconds;
            while (Time.realtimeSinceStartup < until && cast.Any(person => CharacterPortraits.Get(person) == null))
            {
                float next = Time.realtimeSinceStartup + 0.1f;
                while (Time.realtimeSinceStartup < next) yield return null;
            }
            Assert.That(cast.Where(person => CharacterPortraits.Get(person) == null).Select(person => person.name), Is.Empty,
                "Every houseguest's portrait is built before the frames that show them are taken.");
        }

        /// <summary>
        /// Where a piece of an overlay stands in the frame being photographed, in the frame's pixels,
        /// shrunk about its centre to the given share of its width and height. Only inside a capture's
        /// inspection, while the overlays are drawn into the frame through the capture's lens: its
        /// corners go through the camera its canvas is drawn with (<see cref="LensOf"/>).
        /// </summary>
        private Rect CaptureRectOf(Transform node, float widthShare = 1f, float heightShare = 1f)
        {
            var corners = new Vector3[4];
            ((RectTransform)node).GetWorldCorners(corners);
            var camera = LensOf(node);
            var min = new Vector2(float.MaxValue, float.MaxValue);
            var max = new Vector2(float.MinValue, float.MinValue);
            foreach (var corner in corners)
            {
                var point = RectTransformUtility.WorldToScreenPoint(camera, corner);
                min = Vector2.Min(min, point);
                max = Vector2.Max(max, point);
            }
            var size = new Vector2((max.x - min.x) * widthShare, (max.y - min.y) * heightShare);
            return new Rect((min + max) * 0.5f - size * 0.5f, size);
        }

        /// <summary>
        /// Fails when a portrait's face is not in the frame: the middle of its disc - well inside the
        /// ring, where nothing but the face is drawn - is one flat colour. A face that never landed
        /// leaves the white disc it lands in, which a whole-frame check reads as content like any other.
        /// </summary>
        private void AssertFaceIsDrawn(Texture2D frame, Transform portrait, string what)
        {
            Assert.That(portrait, Is.Not.Null, what + ": there is no portrait.");
            var face = portrait.Find("Frame/Face");
            Assert.That(face, Is.Not.Null, what + ": the portrait has nowhere to draw a face.");
            AssertRegionHasContent(frame, CaptureRectOf(face, 0.5f, 0.5f), what);
        }

        /// <summary>
        /// Who came through the front door, and how many times it opened, watched every frame after
        /// the sequence has had its turn - whatever the test is busy with, a capture included.
        /// </summary>
        private sealed class RevealWatcher : MonoBehaviour
        {
            public OpeningSequence Sequence;
            public OpeningDoorSet Door;
            public readonly List<string> Revealed = new List<string>();
            public int DoorOpenings;
            private bool wasOpen;

            private void LateUpdate()
            {
                if (Sequence == null) return;
                string id = Sequence.CurrentGuestId;
                if (id != null && Sequence.CurrentBeat == OpeningBeat.Intro && (Revealed.Count == 0 || Revealed[Revealed.Count - 1] != id))
                    Revealed.Add(id);
                bool open = Door != null && Door.IsOpen;
                if (open && !wasOpen) DoorOpenings++;
                wasOpen = open;
            }
        }
    }
}
