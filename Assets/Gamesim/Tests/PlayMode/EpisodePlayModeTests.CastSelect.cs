using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Gamesim.Persistence;
using Gamesim.Presentation;
using Gamesim.Simulation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Gamesim.Tests.PlayMode
{
    /// <summary>
    /// The cast screen, driven through its own controls.
    ///
    /// <para>The screen's whole reason to exist is that it decides nothing until the player says so.
    /// "New season" used to write a slot the instant it was clicked; now it opens a chooser, and the
    /// failure that would matter most is a cancel that has already replaced the running season —
    /// there is no undo for that. Most of what is here checks that nothing happened.</para>
    /// </summary>
    public sealed partial class EpisodePlayModeTests
    {
        private CastSelect CastScreen() => director.GetComponentInChildren<CastSelect>(true);

        /// <summary>Live, interactable buttons on the screen carrying exactly this caption.</summary>
        private Button[] CastButtons(string caption) =>
            director.GetComponentsInChildren<Button>(true)
                .Where(button => button.IsActive() && button.IsInteractable()
                    && button.GetComponentsInChildren<TMPro.TMP_Text>(true).Any(label => label.text == caption))
                .ToArray();

        /// <summary>
        /// Photographs the cast screen so its quality can be judged from a frame.
        ///
        /// <para>Every other check on this screen asks whether a control exists and does the right
        /// thing when pressed, which is why a screen can pass all of them and still be reported as
        /// low quality. None of them can see a ring that is the wrong colour, a card carrying five
        /// lines where the reference carries two, or a 1180-wide column of content adrift in a
        /// 1920-wide frame.</para>
        /// </summary>
        [UnityTest]
        public IEnumerator CastSelect_CapturesTheScreenForReview()
        {
            if (!Application.isBatchMode) yield break;
            yield return OpenCastScreen();
            Canvas.ForceUpdateCanvases();
            yield return null;
            Assert.That(CastScreen(), Is.Not.Null);
            Assert.That(CastScreen().IsShowing, Is.True, "There is nothing to photograph if it never opened.");

            yield return WaitForCastFaces("cast-select");

            Canvas.ForceUpdateCanvases();
            yield return null;
            yield return CaptureFraming("cast-select");
            CastScreen().Dismiss();
            yield return null;
        }

        /// <summary>
        /// The same screen with a card picked - the state the frame above never shows, and the one
        /// the glass on this screen exists for: the halo, a single ring and "PLAYING AS" on one card
        /// among twelve. Review evidence, not proof; the proof is
        /// <c>CastSelect_CardsAreGlassAndOnlyTheChosenOneGlows</c>.
        /// </summary>
        [UnityTest]
        public IEnumerator CastSelect_CapturesThePickedCardForReview()
        {
            if (!Application.isBatchMode) yield break;
            yield return OpenCastScreen();
            Canvas.ForceUpdateCanvases();
            yield return null;
            Assert.That(CastScreen().IsShowing, Is.True, "There is nothing to photograph if it never opened.");
            yield return WaitForCastFaces("cast-select-picked, before the pick");

            var chosen = CastTemplates.In(CastTemplates.Roster.Regular).First();
            CastButtons(chosen.Name)[0].onClick.Invoke();
            yield return null;
            yield return null;
            // Again, and the picked houseguest's model: a frame taken while it builds shows an empty stage.
            yield return WaitForCastFaces("cast-select-picked");
            float built = Time.realtimeSinceStartup + 25f;
            while (!CastScreen().PreviewReady && Time.realtimeSinceStartup < built) yield return null;

            Canvas.ForceUpdateCanvases();
            yield return null;
            yield return CaptureFraming("cast-select-picked");
            CastScreen().Dismiss();
            yield return null;
        }

        /// <summary>
        /// Waits for the faces. A card with no glamour photo carries a RawImage that CastSelect.Update
        /// fills from CharacterPortraits as its render lands, so a frame taken on open would
        /// photograph an empty disc and read as proof the screen has no portrait.
        /// </summary>
        private IEnumerator WaitForCastFaces(string frame)
        {
            RawImage[] Faces() => CastScreen().GetComponentsInChildren<RawImage>(true)
                .Where(image => image.name == "Model portrait").ToArray();
            int wanted = Faces().Length;
            float deadline = Time.realtimeSinceStartup + 25f;
            while (Time.realtimeSinceStartup < deadline
                   && Faces().Count(image => image.texture != null) < wanted)
                yield return null;
            int landed = Faces().Count(image => image.texture != null);
            Debug.Log("[Gamesim] Cast screen (" + frame + ") - " + landed + " of " + wanted + " portraits rendered before capture.");
        }

        private IEnumerator OpenCastScreen()
        {
            director.OpenSettings();
            yield return null;
            director.NewSeason();
            yield return null;
            yield return null;
        }

        [UnityTest]
        public IEnumerator CastSelect_OpeningTheScreenWritesNothing()
        {
            var before = director.Snapshot;
            string slot = director.SavePath;
            var bytes = File.Exists(slot) ? File.ReadAllBytes(slot) : null;

            yield return OpenCastScreen();

            Assert.That(CastScreen(), Is.Not.Null, "The director must attach a cast screen.");
            Assert.That(CastScreen().IsShowing, Is.True, "New season should open the cast screen.");
            Assert.That(director.SavePath, Is.EqualTo(slot), "Opening the screen must not change slots.");
            Assert.That(director.Snapshot.sessionId, Is.EqualTo(before.sessionId));
            Assert.That(director.Snapshot.contestants.Count, Is.EqualTo(before.contestants.Count));
            if (bytes != null) Assert.That(File.ReadAllBytes(slot), Is.EqualTo(bytes), "The slot on disk must be untouched.");
        }

        [UnityTest]
        public IEnumerator CastSelect_CancellingLeavesTheRunningSeasonExactlyAsItWas()
        {
            var before = director.Snapshot;
            string slot = director.SavePath;

            yield return OpenCastScreen();

            var cancel = CastButtons(CastSelect.CancelCaption);
            Assert.That(cancel, Has.Length.EqualTo(1), "Exactly one cancel control should be reachable.");
            cancel[0].onClick.Invoke();
            yield return null;
            yield return null;

            Assert.That(CastScreen().IsShowing, Is.False, "Cancel must close the screen.");
            Assert.That(director.SavePath, Is.EqualTo(slot));
            Assert.That(director.Snapshot.sessionId, Is.EqualTo(before.sessionId),
                "Cancelling must not replace the running season.");
            Assert.That(director.Snapshot.contestants.Count, Is.EqualTo(before.contestants.Count));
        }

        /// <summary>
        /// Escape closes the cast screen rather than the settings panel behind it. Closing the
        /// panel underneath would leave the screen on top of the house with nothing to go back to.
        /// </summary>
        [UnityTest]
        public IEnumerator CastSelect_EscapeClosesTheScreenAndNotThePanelUnderneath()
        {
            var before = director.Snapshot;
            yield return OpenCastScreen();
            Assert.That(director.IsPanelOpen, Is.True, "The settings panel is open behind the screen.");

            testKeyboard = InputSystem.AddDevice<Keyboard>();
            InputSystem.QueueStateEvent(testKeyboard, new KeyboardState(Key.Escape));
            yield return null;
            InputSystem.QueueStateEvent(testKeyboard, new KeyboardState());
            yield return null; yield return null;

            Assert.That(CastScreen().IsShowing, Is.False, "Escape must close the cast screen.");
            Assert.That(director.IsPanelOpen, Is.True,
                "The first Escape belongs to the screen; the panel behind it stays open.");
            Assert.That(director.Snapshot.sessionId, Is.EqualTo(before.sessionId));

            // And a second Escape, now that the screen is gone, closes the panel as it always did.
            InputSystem.QueueStateEvent(testKeyboard, new KeyboardState(Key.Escape));
            yield return null;
            InputSystem.QueueStateEvent(testKeyboard, new KeyboardState());
            yield return null; yield return null;
            Assert.That(director.IsPanelOpen, Is.False);
        }

        /// <summary>
        /// Committing builds a season of the default size in a new slot, and leaves the old one on
        /// disk. This is the one path that is allowed to write anything.
        /// </summary>
        [UnityTest]
        public IEnumerator CastSelect_StartingBuildsADefaultSizedSeasonInANewSlot()
        {
            var before = director.Snapshot;
            string previousSlot = director.SavePath;
            // Write the slot first. A fresh fixture has a save *path* but no file until something
            // saves, and "the previous slot is retained" is only a claim about a file that exists.
            new EpisodeSaveStore(previousSlot).Save(before);
            Assert.That(File.Exists(previousSlot), Is.True);

            yield return OpenCastScreen();

            var start = CastButtons(CastSelect.StartCaption);
            Assert.That(start, Has.Length.EqualTo(1), "Exactly one start control should be reachable.");
            start[0].onClick.Invoke();
            yield return null;
            yield return null;

            Assert.That(CastScreen().IsShowing, Is.False);
            Assert.That(director.SavePath, Is.Not.EqualTo(previousSlot), "A new season goes in a new slot.");
            Assert.That(File.Exists(previousSlot), Is.True, "The previous slot must be retained.");

            var fresh = director.Snapshot;
            Assert.That(fresh.sessionId, Is.Not.EqualTo(before.sessionId));
            Assert.That(fresh.contestants.Count, Is.EqualTo(SeasonBuilder.DefaultHouseSize));
            Assert.That(fresh.contestants.Count(c => c.isPlayer), Is.EqualTo(1));
            Assert.That(fresh.phase, Is.EqualTo(EpisodePhase.Social));
            Assert.That(fresh.week, Is.EqualTo(1));
            Assert.That(EpisodeValidation.TryValidate(fresh, out var error), Is.True, error);
        }

        [UnityTest]
        public IEnumerator CastSelect_FailedSlotCreationShowsItsReasonAndRetriesTheEditedCast()
        {
            var draft = CharacterDraft.FromAppearance(CastTemplates.Find("emma-brown"));
            draft.Name = "Library guest";
            var profile = CharacterProfile.FromDraft(System.Guid.NewGuid().ToString("N"), draft);
            Assert.That(CastScreen().ProfileStore.Save(profile, out var profileError), Is.True, profileError);

            yield return OpenCastScreen();
            CastButtons(CastTemplates.RosterName(CastTemplates.Roster.AllStars))[0].onClick.Invoke();
            CastButtons("More houseguests")[0].onClick.Invoke();
            CastButtons("Cast slots")[0].onClick.Invoke();
            CastButtons("Add Library guest to the cast")[0].onClick.Invoke();
            CastButtons("Add Library guest to the cast")[0].onClick.Invoke();
            CastButtons("Edit slot 1")[0].onClick.Invoke();
            CastButtons("Identity")[0].onClick.Invoke();
            Creator().GetComponentsInChildren<TMPro.TMP_InputField>()
                .Single(field => field.name == "Name field").text = "Retry guest";
            CastButtons(CharacterCreator.ApplySlotCaption)[0].onClick.Invoke();
            yield return null;
            Assert.That(CastScreen().IsShowing, Is.True);
            Assert.That(Creator().IsShowing, Is.False);

            director.SaveNow();
            var before = director.Snapshot;
            string previousPath = director.SavePath;
            byte[] previousBytes = File.ReadAllBytes(previousPath);
            string blockedRoot = Path.Combine(temporaryDirectory, "blocked-cast-slot-root");
            File.WriteAllText(blockedRoot, "test-owned file prevents creating a save directory");
            var rootField = director.GetType().GetField("saveRoot",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            Assert.That(rootField, Is.Not.Null);
            rootField.SetValue(director, blockedRoot);
            try
            {
                CastButtons(CastSelect.StartCaption)[0].onClick.Invoke();
                yield return null;
                yield return null;

                Assert.That(CastScreen().IsShowing, Is.True, "The failed default-newcomer start returns to its cast setup.");
                Assert.That(Creator().IsShowing, Is.False, "Editing an NPC must not make that NPC the player draft.");
                Assert.That(director.StatusMessage, Does.StartWith("New season could not be saved."));
                var visibleError = CastScreen().GetComponentsInChildren<TMPro.TMP_Text>()
                    .Single(label => label.gameObject.activeInHierarchy && label.text == director.StatusMessage);
                Assert.That(visibleError.transform.parent.name, Is.EqualTo("Fixed season footer"),
                    "The failure must be on the active modal beside retry, outside the scrolling cast list.");
                Assert.That(CastButtons(CastSelect.StartCaption), Has.Length.EqualTo(1));
                Assert.That(CastButtons("Edit slot 1"), Has.Length.EqualTo(1));
                Assert.That(CastButtons("Edit slot 2"), Has.Length.EqualTo(1));
                Assert.That(CastScreen().GetComponentsInChildren<TMPro.TMP_Text>()
                    .Any(label => label.text == "Slot 1: Retry guest"), Is.True);
                Assert.That(director.SavePath, Is.EqualTo(previousPath));
                AssertEquivalent(before, director.Snapshot);
                Assert.That(File.ReadAllBytes(previousPath), Is.EqualTo(previousBytes));
            }
            finally
            {
                rootField.SetValue(director, temporaryDirectory);
            }

            CastButtons(CastSelect.StartCaption)[0].onClick.Invoke();
            yield return null;
            yield return null;
            Assert.That(CastScreen().IsShowing, Is.False);
            var fresh = director.Snapshot;
            Assert.That(fresh.contestants, Has.Count.EqualTo(SeasonBuilder.DefaultHouseSize + 1));
            Assert.That(fresh.Find(fresh.playerId).name, Is.EqualTo("You"));
            Assert.That(fresh.Find("custom-1").name, Is.EqualTo("Retry guest"));
            Assert.That(fresh.Find("custom-2").name, Is.EqualTo("Library guest"));
            var allStars = CastTemplates.In(CastTemplates.Roster.AllStars).Select(template => template.Id).ToArray();
            Assert.That(fresh.contestants.Where(person => !person.isPlayer && !person.id.StartsWith("custom-"))
                .All(person => allStars.Contains(person.id)), Is.True, "Retry must retain the selected roster.");
            Assert.That(File.ReadAllBytes(previousPath), Is.EqualTo(previousBytes));
            Assert.That(CastScreen().ProfileStore.TryLoad(profile.id, out var savedProfile, out profileError), Is.True, profileError);
            Assert.That(savedProfile.name, Is.EqualTo("Library guest"), "Cast edits remain independent of the library profile.");
        }

        /// <summary>
        /// Every houseguest in the built season gets a body, and none of the old season's bodies are
        /// left standing in the house as nameless extras.
        ///
        /// <para>The house was authored for six and the default season is eight, so this is the
        /// first time a season has ever installed a cast larger than the scene's own.</para>
        /// </summary>
        [UnityTest]
        public IEnumerator CastSelect_ABuiltSeasonGivesEveryHouseguestABody()
        {
            yield return OpenCastScreen();
            CastButtons(CastSelect.StartCaption)[0].onClick.Invoke();
            yield return null;
            yield return null;
            yield return SettleCast();

            var state = director.Snapshot;
            var cast = state.contestants.Where(c => !c.isPlayer).Select(c => c.id).OrderBy(id => id).ToArray();
            // The player has a body too, and it is not one of the houseguest slots this fits.
            var bodies = SceneComponents<CharacterPresentation>()
                .Where(visual => visual.isActiveAndEnabled && visual.CharacterId != state.playerId)
                .Select(visual => visual.CharacterId)
                .OrderBy(id => id)
                .ToArray();

            CollectionAssert.AreEqual(cast, bodies,
                "The bodies in the house must be exactly the season's cast — no missing houseguest, "
                + "and nobody left over from the season before.");
        }

        /// <summary>
        /// The screen's card grid covers the whole roster, and every card is a real button rather
        /// than a decorated panel — the grid has to be usable without a mouse.
        /// </summary>
        [UnityTest]
        public IEnumerator CastSelect_EveryHouseguestOnTheRosterHasAReachableCard()
        {
            yield return OpenCastScreen();

            foreach (var template in CastTemplates.In(CastTemplates.Roster.Regular))
                Assert.That(CastButtons(template.Name), Has.Length.EqualTo(1),
                    template.Name + " has no reachable card on the default roster.");

            // And nobody from the other roster is on screen at the same time.
            foreach (var template in CastTemplates.In(CastTemplates.Roster.AllStars))
                Assert.That(CastButtons(template.Name), Is.Empty,
                    template.Name + " is an all-star and should not be on the regular roster.");
        }

        /// <summary>
        /// Picking a card makes that person the player, without putting two of them in the house.
        /// </summary>
        [UnityTest]
        public IEnumerator CastSelect_PickingACardMakesThatHouseguestThePlayer()
        {
            yield return OpenCastScreen();

            var chosen = CastTemplates.In(CastTemplates.Roster.Regular).First();
            CastButtons(chosen.Name)[0].onClick.Invoke();
            yield return null;
            yield return null;

            CastButtons(CastSelect.StartCaption)[0].onClick.Invoke();
            yield return null;
            yield return null;

            var fresh = director.Snapshot;
            var you = fresh.Find(fresh.playerId);
            Assert.That(you, Is.Not.Null);
            Assert.That(you.isPlayer, Is.True);
            Assert.That(you.name, Is.EqualTo(chosen.Name), "The picked card should be the player.");
            Assert.That(fresh.contestants.Count(c => c.name == chosen.Name), Is.EqualTo(1),
                "The picked houseguest must not also be cast as an NPC.");
            Assert.That(EpisodeValidation.TryValidate(fresh, out var error), Is.True, error);
        }

        /// <summary>
        /// Every card shows that houseguest's own glamour photo - the web game's portrait - in its ring,
        /// on both rosters: not a render, not a stand-in, and nobody else's.
        /// </summary>
        [UnityTest]
        public IEnumerator CastSelect_EveryCardShowsThatHouseguestsGlamourPhoto()
        {
            yield return OpenCastScreen();
            foreach (var roster in new[] { CastTemplates.Roster.Regular, CastTemplates.Roster.AllStars })
            {
                CastButtons(CastTemplates.RosterName(roster))[0].onClick.Invoke();
                yield return null;
                foreach (var template in CastTemplates.In(roster))
                {
                    var card = CastScreen().GetComponentsInChildren<RectTransform>().Single(rect => rect.name == template.Name);
                    var photo = card.GetComponentsInChildren<RawImage>().SingleOrDefault(image => image.name == CastSelect.GlamourPhotoName);
                    Assert.That(photo, Is.Not.Null, template.Name + " has a glamour photo on their card.");
                    Assert.That(photo.texture, Is.Not.Null);
                    Assert.That(photo.texture.name, Is.EqualTo(template.Id), template.Name + " wears their own photo.");
                }
            }
        }

        /// <summary>
        /// The details wait for a pick, then show that houseguest - their nickname, home and words -
        /// and their live model, which appears nowhere else on the screen. Picking someone else
        /// changes the model; picking them again puts the details away.
        /// </summary>
        [UnityTest]
        public IEnumerator CastSelect_PickingAPortraitShowsTheirDetailsAndTheirModel()
        {
            yield return OpenCastScreen();
            RawImage Model() => CastScreen().GetComponentsInChildren<RawImage>().SingleOrDefault(image => image.name == CastSelect.LiveModelName);
            string[] Words() => CastScreen().GetComponentsInChildren<RectTransform>().Single(rect => rect.name == CastSelect.DetailPanelName)
                .GetComponentsInChildren<TMPro.TMP_Text>().Select(label => label.text).ToArray();
            Assert.That(Model(), Is.Null, "No model until a houseguest is picked.");
            Assert.That(Words(), Does.Contain("Select a houseguest"));

            var emma = CastTemplates.Find("emma-brown");
            CastButtons(emma.Name)[0].onClick.Invoke();
            yield return null;
            Assert.That(Words(), Does.Contain(emma.Archetype), "Her nickname.");
            Assert.That(Words(), Does.Contain(emma.Occupation));
            Assert.That(Words().Any(word => word.Contains(emma.Hometown)), Is.True, "Where she is from.");
            Assert.That(Words().Any(word => word.Contains(emma.Bio)), Is.True, "Her words, quoted.");
            Assert.That(Words(), Does.Contain(CastTemplates.CategoryDescription(emma.Category)), "What her kind of player is.");
            Assert.That(CastButtons("Play as Emma"), Has.Length.EqualTo(1));
            Assert.That(CastButtons(emma.Name), Has.Length.EqualTo(1), "The details add no second control with her name.");
            Assert.That(Model(), Is.Not.Null, "Her model stands in the details.");
            Assert.That(CastScreen().PreviewedId, Is.EqualTo(emma.Id));
            float built = Time.realtimeSinceStartup + 25f;
            while (!CastScreen().PreviewReady && Time.realtimeSinceStartup < built) yield return null;
            Assert.That(CastScreen().PreviewReady, Is.True, "Her model is built and drawn.");
            // What the studio actually finished building, not the id the screen set when she was
            // picked: a screen that never asked the studio for her, or asked for the wrong look,
            // passed everything above. And a real body where one can be built - ready is also what
            // a timed-out build that swapped in the placeholder capsule reports.
            Assert.That(CastScreen().StudioPreview.CompletedKey, Is.EqualTo(CharacterAppearance.Preset(emma.Id).ContentKey()),
                "The model built is Emma's own look.");
            if (CharacterBodySource.Provider != null)
                Assert.That(CastScreen().StudioPreview.CanRetry, Is.False, "Emma's model is her body, not a fallback: "
                    + CastScreen().StudioPreview.Status);

            var casey = CastTemplates.Find("casey-wilson");
            CastButtons(casey.Name)[0].onClick.Invoke();
            yield return null;
            Assert.That(CastScreen().PreviewedId, Is.EqualTo(casey.Id), "Picking someone else shows their model.");
            built = Time.realtimeSinceStartup + 25f;
            while (!CastScreen().PreviewReady && Time.realtimeSinceStartup < built) yield return null;
            Assert.That(CastScreen().PreviewReady, Is.True, "Casey's model is built and drawn.");
            Assert.That(CastScreen().StudioPreview.CompletedKey, Is.EqualTo(CharacterAppearance.Preset(casey.Id).ContentKey()),
                "and the studio rebuilt it as Casey, rather than going on showing Emma.");
            CastButtons(casey.Name)[0].onClick.Invoke();
            yield return null;
            Assert.That(Model(), Is.Null, "Picking them again puts the details away.");
            Assert.That(CastScreen().PreviewedId, Is.Null);
        }

        /// <summary>Play as - the details' commit - starts the season as that houseguest, like Start.</summary>
        [UnityTest]
        public IEnumerator CastSelect_PlayAsStartsTheSeasonAsThatHouseguest()
        {
            yield return OpenCastScreen();
            var will = CastTemplates.Find("dr-will-kirby");
            CastButtons(CastTemplates.RosterName(CastTemplates.Roster.AllStars))[0].onClick.Invoke();
            yield return null;
            CastButtons(will.Name)[0].onClick.Invoke();
            yield return null;
            var play = CastButtons("Play as Will");
            Assert.That(play, Has.Length.EqualTo(1), "An honorific is not a first name.");
            play[0].onClick.Invoke();
            yield return null;
            yield return null;
            Assert.That(CastScreen().IsShowing, Is.False);
            var fresh = director.Snapshot;
            Assert.That(fresh.Find(fresh.playerId).name, Is.EqualTo(will.Name));
            Assert.That(fresh.contestants.Count(person => person.name == will.Name), Is.EqualTo(1));
        }

        /// <summary>
        /// "Play as Emma" plays as Emma, whoever else the player built on the way. Building Robin
        /// in the creator and coming back leaves Robin retained - the footer says so, and Start
        /// commits Robin - but it used to be what Play as committed too, so the season started with
        /// Robin as the player and Emma cast as one of the others.
        /// </summary>
        [UnityTest]
        public IEnumerator CastSelect_PlayAsPlaysTheHouseguestItNamesNotADraftOfSomeoneElse()
        {
            yield return OpenCastScreen();
            var emma = CastTemplates.Find("emma-brown");
            CastButtons(emma.Name)[0].onClick.Invoke();
            yield return null;
            CastButtons(CharacterCreator.CreateCaption)[0].onClick.Invoke();
            yield return null;
            Assert.That(Creator().IsShowing, Is.True);
            Creator().Draft.Name = "Robin";
            CastButtons(CharacterCreator.BackCaption)[0].onClick.Invoke();
            yield return null;
            Assert.That(CastScreen().IsShowing, Is.True);
            Assert.That(CastScreen().GetComponentsInChildren<TMPro.TMP_Text>().Any(label => label.text.Contains("You will play as Robin")), Is.True,
                "Robin is retained and named in the footer - the case this is about.");

            var play = CastButtons("Play as Emma");
            Assert.That(play, Has.Length.EqualTo(1), "The details still offer Emma, under her own name.");
            play[0].onClick.Invoke();
            yield return null;
            yield return null;

            Assert.That(CastScreen().IsShowing, Is.False, "The season started.");
            var fresh = director.Snapshot;
            Assert.That(fresh.Find(fresh.playerId).name, Is.EqualTo(emma.Name), "Play as Emma plays as Emma.");
            Assert.That(fresh.contestants.Count(person => person.name == emma.Name), Is.EqualTo(1), "and she is not also an NPC.");
            Assert.That(fresh.contestants.Any(person => person.name == "Robin"), Is.False, "Robin was never committed.");
        }

        /// <summary>
        /// Edits to a houseguest brought back from the creator stay theirs: the live model wears
        /// them, "Customize Emma" reopens them rather than starting again from her card - which
        /// used to throw the edits away at the next Back - and "Play as Emma" plays her in them.
        /// </summary>
        [UnityTest]
        public IEnumerator CastSelect_CustomizeReopensTheEditsBroughtBackForThatHouseguest()
        {
            yield return OpenCastScreen();
            var emma = CastTemplates.Find("emma-brown");
            CastButtons(emma.Name)[0].onClick.Invoke();
            yield return null;
            CastButtons("Customize Emma")[0].onClick.Invoke();
            yield return null;
            Assert.That(Creator().IsShowing, Is.True);
            CastButtons("Next starting look")[0].onClick.Invoke();
            yield return null;
            string edited = Creator().Draft.Appearance.ContentKey();
            string editedPreset = Creator().Draft.Appearance.presetId;
            Assert.That(edited, Is.Not.EqualTo(CharacterAppearance.Preset(emma.Id).ContentKey()), "The look really changed.");
            Assert.That(editedPreset, Is.Not.EqualTo(emma.Id));
            CastButtons(CharacterCreator.BackCaption)[0].onClick.Invoke();
            yield return null;
            Assert.That(CastScreen().IsShowing, Is.True);

            float built = Time.realtimeSinceStartup + 25f;
            while (!CastScreen().PreviewReady && Time.realtimeSinceStartup < built) yield return null;
            Assert.That(CastScreen().PreviewReady, Is.True);
            Assert.That(CastScreen().StudioPreview.CompletedKey, Is.EqualTo(edited),
                "The live model shows Emma as the player left her, not her card's look.");

            CastButtons("Customize Emma")[0].onClick.Invoke();
            yield return null;
            Assert.That(Creator().IsShowing, Is.True);
            Assert.That(Creator().Draft.Appearance.ContentKey(), Is.EqualTo(edited), "Customize reopens the edits.");
            CastButtons(CharacterCreator.BackCaption)[0].onClick.Invoke();
            yield return null;

            CastButtons("Play as Emma")[0].onClick.Invoke();
            yield return null;
            yield return null;
            Assert.That(CastScreen().IsShowing, Is.False, "The season started.");
            var fresh = director.Snapshot;
            var you = fresh.Find(fresh.playerId);
            Assert.That(you.name, Is.EqualTo(emma.Name));
            Assert.That(you.appearance.presetId, Is.EqualTo(editedPreset), "Play as Emma plays her in the edited look.");
            Assert.That(fresh.contestants.Count(person => person.name == emma.Name), Is.EqualTo(1));
        }

        /// <summary>
        /// A "Play as" that cannot be saved comes back to the cast screen, with the reason beside
        /// retry. Every card the screen starts carries a built houseguest, so the director used to
        /// guess the creator: never opened, that left the player on no screen at all; opened
        /// earlier, it came back with its own stale choice, whose Start would commit a season the
        /// player had moved on from. Both are checked.
        /// </summary>
        [UnityTest]
        public IEnumerator CastSelect_AFailedPlayAsComesBackToTheCastScreen()
        {
            yield return OpenCastScreen();
            var emma = CastTemplates.Find("emma-brown");
            CastButtons(emma.Name)[0].onClick.Invoke();
            yield return null;
            yield return PlayAsWithTheSaveRootBlocked("Play as Emma");

            // Now with the creator opened and backed out of first, on another houseguest.
            CastButtons("Customize Emma")[0].onClick.Invoke();
            yield return null;
            Assert.That(Creator().IsShowing, Is.True);
            CastButtons(CharacterCreator.BackCaption)[0].onClick.Invoke();
            yield return null;
            var casey = CastTemplates.Find("casey-wilson");
            CastButtons(casey.Name)[0].onClick.Invoke();
            yield return null;
            yield return PlayAsWithTheSaveRootBlocked("Play as Casey");

            // And retry, with the disk back, starts the season it says.
            CastButtons("Play as Casey")[0].onClick.Invoke();
            yield return null;
            yield return null;
            Assert.That(CastScreen().IsShowing, Is.False);
            Assert.That(Creator().IsShowing, Is.False);
            var fresh = director.Snapshot;
            Assert.That(fresh.Find(fresh.playerId).name, Is.EqualTo(casey.Name));
        }

        private IEnumerator PlayAsWithTheSaveRootBlocked(string caption)
        {
            var before = director.Snapshot;
            string previousPath = director.SavePath;
            string blockedRoot = Path.Combine(temporaryDirectory, "blocked-play-as-" + System.Guid.NewGuid().ToString("N"));
            File.WriteAllText(blockedRoot, "test-owned file prevents creating a save directory");
            var rootField = director.GetType().GetField("saveRoot",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            Assert.That(rootField, Is.Not.Null);
            rootField.SetValue(director, blockedRoot);
            try
            {
                var play = CastButtons(caption);
                Assert.That(play, Has.Length.EqualTo(1), caption + " is offered.");
                play[0].onClick.Invoke();
                yield return null;
                yield return null;

                Assert.That(director.StatusMessage, Does.StartWith("New season could not be saved."));
                Assert.That(CastScreen().IsShowing, Is.True, "A failed " + caption + " comes back to the cast screen.");
                Assert.That(Creator().IsShowing, Is.False, "and not to the creator, which did not commit it.");
                var visibleError = CastScreen().GetComponentsInChildren<TMPro.TMP_Text>()
                    .Single(label => label.gameObject.activeInHierarchy && label.text == director.StatusMessage);
                Assert.That(visibleError.transform.parent.name, Is.EqualTo("Fixed season footer"),
                    "The reason is in the footer, beside retry.");
                Assert.That(CastButtons(caption), Has.Length.EqualTo(1), "Retry is where it was.");
                Assert.That(director.SavePath, Is.EqualTo(previousPath));
                Assert.That(director.Snapshot.sessionId, Is.EqualTo(before.sessionId), "The running season is untouched.");
            }
            finally
            {
                rootField.SetValue(director, temporaryDirectory);
            }
        }

        /// <summary>
        /// Starting or cancelling lets the live model go: the studio, its built body and its
        /// multisampled target. Hiding only put the studio to sleep, and nothing else destroyed it,
        /// so the last houseguest previewed stayed resident for the whole season - from a start on
        /// this screen, and from a start in the creator it opened, which leaves it asleep behind.
        /// </summary>
        [UnityTest]
        public IEnumerator CastSelect_ClosingTheScreenLetsTheLiveModelGo()
        {
            // Counted against what was there before, since a studio lives at the scene root and
            // outlasts a scene load unless something destroys it.
            int Studios() => Resources.FindObjectsOfTypeAll<CharacterStudioPreview>()
                .Count(item => item != null && item.name == CastSelect.StudioName);
            int already = Studios();
            yield return OpenCastScreen();
            var emma = CastTemplates.Find("emma-brown");
            CastButtons(emma.Name)[0].onClick.Invoke();
            yield return null;
            Assert.That(Studios(), Is.EqualTo(already + 1), "Picking a houseguest stands their model in a studio.");
            CastButtons(CastSelect.CancelCaption)[0].onClick.Invoke();
            yield return null;
            Assert.That(CastScreen().IsShowing, Is.False);
            Assert.That(Studios(), Is.EqualTo(already), "Cancel lets the model go.");

            director.NewSeason();
            yield return null;
            CastButtons(emma.Name)[0].onClick.Invoke();
            yield return null;
            Assert.That(Studios(), Is.EqualTo(already + 1));
            CastButtons("Play as Emma")[0].onClick.Invoke();
            yield return null;
            yield return null;
            Assert.That(CastScreen().IsShowing, Is.False, "The season started.");
            Assert.That(Studios(), Is.EqualTo(already), "Starting lets the model go too.");

            // And a start from the creator, which hides this screen rather than closing it: nothing
            // brings the screen back to the houseguest it was showing.
            director.NewSeason();
            yield return null;
            CastButtons(emma.Name)[0].onClick.Invoke();
            yield return null;
            Assert.That(Studios(), Is.EqualTo(already + 1));
            CastButtons("Customize Emma")[0].onClick.Invoke();
            yield return null;
            Assert.That(Creator().IsShowing, Is.True);
            CastButtons(CharacterCreator.StartCaption)[0].onClick.Invoke();
            yield return null;
            yield return null;
            Assert.That(Creator().IsShowing, Is.False, "The season started from the creator.");
            Assert.That(CastScreen().IsShowing, Is.False);
            Assert.That(Studios(), Is.EqualTo(already), "and the model the cast screen was showing went with it.");
        }

        /// <summary>
        /// Play as and Customize stay inside the details, clear of Start and Cancel, on the short
        /// wide frames that broke them. The details needed a fixed 448 at least, so at 21:9 with
        /// the larger text (a 375 panel) and at 32:9 with the standard text both buttons sat on the
        /// footer, which drew and took clicks over them.
        /// </summary>
        [UnityTest]
        public IEnumerator CastSelect_PlayAsAndCustomizeStayInsideTheDetailsOnShortWideFrames()
        {
            yield return OpenCastScreen();
            var emma = CastTemplates.Find("emma-brown");
            CastButtons(emma.Name)[0].onClick.Invoke();
            yield return null;
            var canvas = CastScreen().GetComponent<Canvas>();
            try
            {
                var shapes = new[]
                {
                    (wide: 2560, high: 1080, text: 1.2f, shape: "21:9 at the larger text size"),
                    (wide: 5120, high: 1440, text: 1f, shape: "32:9 at the standard text size"),
                    (wide: 1920, high: 1080, text: 1.2f, shape: "16:9 at the larger text size"),
                };
                foreach (var frame in shapes)
                {
                    yield return LayCastScreenOutAt(frame.wide, frame.high, frame.text);
                    var panel = ScreenRect(ActiveCastRect(CastSelect.DetailPanelName));
                    var start = ScreenRect((RectTransform)CastButtons(CastSelect.StartCaption).Single().transform);
                    var cancel = ScreenRect((RectTransform)CastButtons(CastSelect.CancelCaption).Single().transform);
                    foreach (string caption in new[] { "Play as Emma", "Customize Emma" })
                    {
                        var buttons = CastButtons(caption);
                        Assert.That(buttons, Has.Length.EqualTo(1), caption + " on " + frame.shape);
                        var button = ScreenRect((RectTransform)buttons[0].transform);
                        Assert.That(Encloses(panel, button), Is.True,
                            caption + " at " + button + " spills out of the details at " + panel + " on " + frame.shape + ".");
                        Assert.That(button.Overlaps(start), Is.False, caption + " lies on Start on " + frame.shape + ".");
                        Assert.That(button.Overlaps(cancel), Is.False, caption + " lies on Cancel on " + frame.shape + ".");
                    }
                    // Room was made without taking her away: the model is still standing there.
                    var model = ActiveCastRect(CastSelect.LiveModelName);
                    Assert.That(Encloses(panel, ScreenRect(model)), Is.True, "The model is inside the details on " + frame.shape + ".");
                    Assert.That(model.rect.height, Is.GreaterThanOrEqualTo(79f), "and tall enough to be somebody on " + frame.shape + ".");
                }
            }
            finally
            {
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            }
            CastScreen().Dismiss();
            yield return null;
        }

        /// <summary>
        /// At the larger text size on 16:9 the third row of cards is under the fold. A scrollbar
        /// says so, and picking a card down there leaves the grid where it was: every press
        /// rebuilds the screen, and the grid used to come back at the top, taking the card just
        /// picked - and its PLAYING AS - out of sight. A new screen starts at the top again, and at
        /// the standard size, where the whole roster fits, there is no scrollbar at all.
        /// </summary>
        [UnityTest]
        public IEnumerator CastSelect_PickingACardBelowTheFoldKeepsTheGridWhereItWas()
        {
            yield return OpenCastScreen();
            var canvas = CastScreen().GetComponent<Canvas>();
            try
            {
                yield return LayCastScreenOutAt(1920, 1080, 1.2f);
                yield return null;
                var scroll = CastGridScroll();
                float most = scroll.content.rect.height - scroll.viewport.rect.height;
                Assert.That(most, Is.GreaterThan(100f), "The third row is below the fold here - the case this is about.");
                Assert.That(scroll.verticalScrollbar, Is.Not.Null, "Something says there is more.");
                Assert.That(scroll.verticalScrollbar.gameObject.activeInHierarchy, Is.True, "and it is showing.");

                scroll.content.anchoredPosition = new Vector2(0f, most);
                yield return null;
                var third = CastTemplates.Filter(CastTemplates.Roster.Regular, CastTemplates.AllCategories).ElementAt(8);
                CastButtons(third.Name)[0].onClick.Invoke();
                yield return null;

                scroll = CastGridScroll();
                Assert.That(scroll.content.anchoredPosition.y, Is.EqualTo(most).Within(1f), "The grid stays where the player scrolled it.");
                var card = (RectTransform)scroll.content.Find(third.Name);
                Assert.That(card, Is.Not.Null);
                Assert.That(Encloses(ScreenRect(scroll.viewport), ScreenRect(card)), Is.True,
                    third.Name + ", just picked, is still in view.");

                CastScreen().Dismiss();
                yield return null;
                director.NewSeason();
                yield return null;
                Assert.That(CastGridScroll().content.anchoredPosition.y, Is.EqualTo(0f).Within(.5f),
                    "Opening the screen again starts at the top row.");

                // And on the frame the grid is drawn for - 16:9 at the standard size - all twelve
                // fit, so nothing scrolls and no bar says there is more. The grid used to end on a
                // gutter and the page's pad, which took it twelve past the viewport.
                yield return LayCastScreenOutAt(1920, 1080, 1f);
                scroll = CastGridScroll();
                float settled = Time.realtimeSinceStartup + 2f;
                while (scroll.verticalScrollbar.gameObject.activeSelf && Time.realtimeSinceStartup < settled) yield return null;
                Assert.That(scroll.content.rect.height, Is.LessThanOrEqualTo(scroll.viewport.rect.height),
                    "The whole roster fits the grid at 16:9.");
                Assert.That(scroll.verticalScrollbar.gameObject.activeSelf, Is.False, "and no scrollbar says otherwise.");
            }
            finally
            {
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            }
            CastScreen().Dismiss();
            yield return null;
        }

        /// <summary>
        /// A card submitted from the keyboard or a pad hands the focus to Play as, the next thing
        /// to press, and clearing the pick leaves it on that card. The press destroys the card it
        /// was made on, and the HUD's ring used to fall back to its first control - the top nav
        /// chip, 25 presses from Play as.
        /// </summary>
        [UnityTest]
        public IEnumerator CastSelect_SubmittingACardPutsTheKeyboardOnPlayAs()
        {
            yield return OpenCastScreen();
            var events = EventSystem.current;
            Assert.That(events, Is.Not.Null, "The house has an event system to submit through.");
            var emma = CastTemplates.Find("emma-brown");
            var card = CastButtons(emma.Name)[0];
            events.SetSelectedGameObject(card.gameObject);
            yield return null;
            Assert.That(events.currentSelectedGameObject, Is.SameAs(card.gameObject), "Emma's card has the focus.");

            ExecuteEvents.Execute(card.gameObject, new BaseEventData(events), ExecuteEvents.submitHandler);
            yield return null;
            yield return null;
            var selected = events.currentSelectedGameObject;
            Assert.That(selected, Is.Not.Null, "Something has the focus after the pick.");
            Assert.That(selected.GetComponentsInChildren<TMPro.TMP_Text>(true).Any(label => label.text == "Play as Emma"), Is.True,
                "The pick hands the focus to Play as, not to " + selected.name + ".");

            card = CastButtons(emma.Name)[0];
            events.SetSelectedGameObject(card.gameObject);
            yield return null;
            ExecuteEvents.Execute(card.gameObject, new BaseEventData(events), ExecuteEvents.submitHandler);
            yield return null;
            yield return null;
            selected = events.currentSelectedGameObject;
            Assert.That(CastButtons("Play as Emma"), Is.Empty, "The pick is cleared.");
            Assert.That(selected, Is.Not.Null);
            Assert.That(selected.name, Is.EqualTo(emma.Name), "Clearing the pick leaves the focus on her card, not on " + selected.name + ".");
            Assert.That(selected.activeInHierarchy, Is.True, "the card as drawn again, not the one the press destroyed.");
        }

        /// <summary>
        /// Under "Reduce character motion" the cast screen holds still: the live model does not
        /// turn by itself, the picked card's halo does not pulse, and a portrait does not lift
        /// under the pointer. The setting reaches the screen from Settings, where the player sets it.
        /// </summary>
        [UnityTest]
        public IEnumerator CastSelect_ReducedMotionHoldsTheModelTheHaloAndThePortraitsStill()
        {
            director.OpenSettings();
            yield return null;
            ButtonWithCaption("Reduce character motion").onClick.Invoke();
            yield return null;
            director.NewSeason();
            yield return null;
            yield return null;
            Assert.That(CastScreen().IsShowing, Is.True);
            Assert.That(CastScreen().ReducedMotion, Is.True, "The setting reaches the cast screen.");

            var emma = CastTemplates.Find("emma-brown");
            float picked = Time.realtimeSinceStartup;
            CastButtons(emma.Name)[0].onClick.Invoke();
            float built = picked + 25f;
            while (!CastScreen().PreviewReady && Time.realtimeSinceStartup < built) yield return null;
            Assert.That(CastScreen().PreviewReady, Is.True);

            // The slow turn starts 2.5 s after the last touch and runs at 14 degrees a second, and
            // the halo pulses once every two seconds: watch past the one and through most of the other.
            var card = CastScreen().GetComponentsInChildren<RectTransform>().Single(rect => rect.name == emma.Name);
            var halo = card.GetComponentsInChildren<Image>().Single(image => image.name == "Ring halo");
            float turn = CastScreen().StudioPreview.Turn;
            var alphas = new List<float>();
            float until = Mathf.Max(picked + 4f, Time.realtimeSinceStartup + 1.5f);
            while (Time.realtimeSinceStartup < until) { alphas.Add(halo.color.a); yield return null; }
            Assert.That(CastScreen().StudioPreview.Turn, Is.EqualTo(turn).Within(.01f), "The model stays as it was left.");
            Assert.That(alphas.Max() - alphas.Min(), Is.LessThan(.001f), "The halo holds still.");

            var portrait = (RectTransform)card.Find("Portrait");
            ExecuteEvents.Execute(card.gameObject, new PointerEventData(EventSystem.current), ExecuteEvents.pointerEnterHandler);
            Assert.That(portrait.localScale, Is.EqualTo(Vector3.one), "The portrait does not lift under the pointer.");
            CastScreen().Dismiss();
            yield return null;
        }

        /// <summary>
        /// The music rule's silent screens are silent however they are reached: the season report
        /// from its button, and the cast screen from the settings panel - neither is a render, and
        /// the rule used to run only on one, so the season's track played on under both.
        /// </summary>
        [UnityTest]
        public IEnumerator CastSelect_TheReportAndTheCastScreenAreSilent()
        {
            var audio = director.GetComponent<HouseAudio>();
            director.OpenSettings();
            yield return null;
            Assert.That(audio.CurrentMusic, Is.Not.EqualTo(HouseAudio.Music.Silent), "The house has its music - the case this is about.");

            director.ShowSeasonReport();
            yield return null;
            Assert.That(director.IsSeasonReportOpen, Is.True);
            Assert.That(audio.CurrentMusic, Is.EqualTo(HouseAudio.Music.Silent), "The final stats are silent.");
            ReportButtons(SeasonReport.CloseCaption)[0].onClick.Invoke();
            yield return null;
            Assert.That(audio.CurrentMusic, Is.Not.EqualTo(HouseAudio.Music.Silent), "Closing the report gives the house its music back.");

            director.NewSeason();
            yield return null;
            Assert.That(CastScreen().IsShowing, Is.True);
            Assert.That(audio.CurrentMusic, Is.EqualTo(HouseAudio.Music.Silent), "Setup is silent, from settings as from the menu.");
            CastButtons(CastSelect.CancelCaption)[0].onClick.Invoke();
            yield return null;
            Assert.That(audio.CurrentMusic, Is.Not.EqualTo(HouseAudio.Music.Silent), "and cancelling it gives the music back.");
        }

        /// <summary>The cast screen's one live element of this name.</summary>
        private RectTransform ActiveCastRect(string name) =>
            CastScreen().GetComponentsInChildren<RectTransform>().Single(rect => rect.name == name);

        /// <summary>The grid's scroll, as drawn now: a rebuild replaces it.</summary>
        private ScrollRect CastGridScroll() => CastScreen().GetComponentsInChildren<ScrollRect>().Single();

        /// <summary>Whether <paramref name="inner"/> lies within <paramref name="outer"/>, to a pixel.</summary>
        private static bool Encloses(Rect outer, Rect inner) =>
            inner.xMin >= outer.xMin - 1f && inner.yMin >= outer.yMin - 1f && inner.xMax <= outer.xMax + 1f && inner.yMax <= outer.yMax + 1f;

        /// <summary>
        /// Lays the cast screen out as a display this many pixels across would, at this text size.
        ///
        /// <para>A batchmode screen is one shape, and the layouts that break are the ones it is not:
        /// short, wide frames. The screen measures its own canvas at every rebuild, so the canvas
        /// is taken out of the screen's hands (world space, where nothing drives its size) and
        /// given the size the scaler would have given it there. The scaler matches width and height
        /// equally against 1920x1080 over the text size, so a W by H display comes to W/k by H/k
        /// with k = sqrt(W*s/1920 * H*s/1080): 21:9 at the larger size is 1848 by 779. The caller
        /// puts the canvas back in overlay.</para>
        /// </summary>
        private IEnumerator LayCastScreenOutAt(int pixelsWide, int pixelsHigh, float textScale)
        {
            var canvas = CastScreen().GetComponent<Canvas>();
            float k = Mathf.Sqrt(pixelsWide * textScale / 1920f * (pixelsHigh * textScale / 1080f));
            var frame = new Vector2(pixelsWide / k, pixelsHigh / k);
            var before = ActiveCastRect(CastSelect.DetailPanelName);
            canvas.renderMode = RenderMode.WorldSpace;
            ((RectTransform)canvas.transform).sizeDelta = frame;
            // The screen sees a new frame in its own Update and lays itself out again; that is the
            // state to wait for, not a number of frames.
            float deadline = Time.realtimeSinceStartup + 5f;
            while (Time.realtimeSinceStartup < deadline
                   && CastScreen().GetComponentsInChildren<RectTransform>().FirstOrDefault(rect => rect.name == CastSelect.DetailPanelName) == before)
                yield return null;
            Canvas.ForceUpdateCanvases();
            Assert.That(ActiveCastRect(CastSelect.DetailPanelName), Is.Not.SameAs(before),
                "The screen laid itself out again for " + pixelsWide + "x" + pixelsHigh + " (" + frame + ").");
            Assert.That(((RectTransform)canvas.transform).rect.size.y, Is.EqualTo(frame.y).Within(.5f));
        }

        /// <summary>A category chip says what that kind of player is, in the web game's words.</summary>
        [UnityTest]
        public IEnumerator CastSelect_ACategoryChipExplainsItsKindOfPlayer()
        {
            yield return OpenCastScreen();
            string Line() => CastScreen().GetComponentsInChildren<TMPro.TMP_Text>().Single(label => label.name == "Category line").text;
            Assert.That(Line(), Does.StartWith("12 houseguests"), "With no filter, how the roster divides.");
            foreach (string kind in CastTemplates.Categories)
            {
                CastButtons(kind)[0].onClick.Invoke();
                yield return null;
                Assert.That(Line(), Does.Contain(CastTemplates.CategoryDescription(kind)), kind);
            }
        }

        /// <summary>
        /// Nothing on the screen takes a raycast once it is hidden. A screen that keeps eating
        /// clicks after it closes is indistinguishable from a frozen game.
        /// </summary>
        [UnityTest]
        public IEnumerator CastSelect_HiddenScreenSwallowsNothing()
        {
            yield return OpenCastScreen();
            CastScreen().Dismiss();
            yield return null;

            var group = CastScreen().GetComponent<CanvasGroup>();
            Assert.That(group.blocksRaycasts, Is.False);
            Assert.That(group.interactable, Is.False);
            Assert.That(group.alpha, Is.EqualTo(0f));
            Assert.That(CastButtons(CastSelect.StartCaption), Is.Empty,
                "A hidden screen must offer no reachable controls.");
        }
    }
}
