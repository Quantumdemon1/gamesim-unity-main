using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Gamesim.Presentation;
using Gamesim.Simulation;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Gamesim.Tests.PlayMode
{
    /// <summary>
    /// The opening sequence, driven directly.
    ///
    /// <para>The director will not start it in batchmode — a sequence that waits is the only thing
    /// that could hold up an automated season — so these drive <see cref="OpeningSequence.Play"/>
    /// the way the tour's own tests drive <c>HouseTutorial.Show</c>.</para>
    ///
    /// <para>What is asserted is sequence <i>state</i> and never pixels. Batchmode renders without
    /// presenting, so a screenshot of any of this is a black rectangle; what actually matters is
    /// which beats were recorded, that the camera was given back, and that skipping is honoured.
    /// </para>
    /// </summary>
    public sealed partial class EpisodePlayModeTests
    {
        private OpeningSequence Opening() => director.GetComponentInChildren<OpeningSequence>(true);

        [UnityTest]
        public IEnumerator Tutorial_IsolatedSessionDoesNotReadOrWritePlayerCompletion()
        {
            var tutorial = director.gameObject.scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<HouseTutorial>(true)).Single();
            bool hadKey = PlayerPrefs.HasKey(HouseTutorial.SeenKey);
            int previous = PlayerPrefs.GetInt(HouseTutorial.SeenKey, -1);
            Assert.That(tutorial.RememberCompletion, Is.False);
            Assert.That(tutorial.HasSeen, Is.False, "An isolated first session has its own completion state.");
            tutorial.Show(_ => null);
            yield return null;
            Assert.That(tutorial.IsShowing, Is.True);
            tutorial.Skip();
            Assert.That(tutorial.IsShowing, Is.False);
            Assert.That(tutorial.HasSeen, Is.True, "Skipping is still remembered for this isolated session.");
            Assert.That(PlayerPrefs.HasKey(HouseTutorial.SeenKey), Is.EqualTo(hadKey));
            Assert.That(PlayerPrefs.GetInt(HouseTutorial.SeenKey, -1), Is.EqualTo(previous));
        }

        /// <summary>
        /// The camera rig, which the director holds a reference to but does not own — it is a scene
        /// object beside the house, so it is not in the director's subtree.
        /// </summary>
        private House.HouseCameraRig Rig() =>
            director.gameObject.scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<House.HouseCameraRig>(true))
                .FirstOrDefault();

        private static OpeningSequence.Settings Plan(List<string> recorded, bool reducedMotion = false) =>
            new OpeningSequence.Settings
            {
                MarkBeat = recorded.Add,
                // No tour: in batchmode there is nobody to show it to, and a beat that waits for a
                // click nobody can make would hang the run.
                RunTutorial = done => done(),
                ReducedMotion = reducedMotion,
                // No arming either: the introductions' controls are live on the frame after they
                // are built, in the editor as in batchmode. The tests of the arming set it themselves.
                ArmSeconds = 0f,
            };

        private static IEnumerator Until(OpeningSequence sequence)
        {
            for (int frame = 0; frame < 600 && sequence.IsPlaying; frame++) yield return null;
            Assert.That(sequence.IsPlaying, Is.False, "The sequence should have finished by now.");
        }

        [UnityTest]
        public IEnumerator Opening_PlaysEveryBeatOnceAndRecordsThemInOrder()
        {
            var sequence = Opening();
            Assert.That(sequence, Is.Not.Null, "The director must attach an opening sequence.");

            var recorded = new List<string>();
            sequence.Play(new string[0], Plan(recorded));
            yield return Until(sequence);

            CollectionAssert.AreEqual(OpeningBeat.InOrder, recorded);
        }

        /// <summary>The ordinary case for every load after the first: there is nothing left to play.</summary>
        [UnityTest]
        public IEnumerator Opening_ASeasonThatHasSeenItAllPlaysNothing()
        {
            var sequence = Opening();
            var recorded = new List<string>();
            bool finished = false;

            var plan = Plan(recorded);
            plan.Finished = () => finished = true;
            sequence.Play(OpeningBeat.InOrder, plan);
            yield return null;

            Assert.That(sequence.IsPlaying, Is.False);
            Assert.That(recorded, Is.Empty);
            Assert.That(finished, Is.True, "The caller still has to be told it is over.");
        }

        [UnityTest]
        public IEnumerator Opening_AHalfSeenSequenceResumesWhereItStopped()
        {
            var sequence = Opening();
            var recorded = new List<string>();
            sequence.Play(new[] { OpeningBeat.Intro, OpeningBeat.HouseEntry }, Plan(recorded));
            yield return Until(sequence);

            CollectionAssert.AreEqual(
                new[] { OpeningBeat.WalkIn, OpeningBeat.Tutorial, OpeningBeat.MeetAndGreet },
                recorded);
        }

        /// <summary>
        /// Skipping records the beats it skipped. Leaving them pending would offer the intro again
        /// on the next load to somebody who has already said no to it.
        /// </summary>
        [UnityTest]
        public IEnumerator Opening_SkippingRecordsEverythingItSkipped()
        {
            var sequence = Opening();
            var recorded = new List<string>();
            sequence.Play(new string[0], Plan(recorded));
            yield return null;

            sequence.Skip();
            yield return null;

            Assert.That(sequence.IsPlaying, Is.False);
            CollectionAssert.AreEquivalent(OpeningBeat.InOrder, recorded);
        }

        [UnityTest]
        public IEnumerator Opening_ReducedMotionStillPlaysEveryBeat()
        {
            var sequence = Opening();
            var recorded = new List<string>();
            sequence.Play(new string[0], Plan(recorded, reducedMotion: true));
            yield return Until(sequence);

            CollectionAssert.AreEqual(OpeningBeat.InOrder, recorded,
                "The preference removes movement, not information.");
        }

        /// <summary>The title card, held long enough to photograph: the frame to judge the opening by.</summary>
        [UnityTest]
        public IEnumerator Opening_CapturesTheTitleForReview()
        {
            var sequence = Opening();
            var recorded = new List<string>();
            var plan = Plan(recorded, reducedMotion: true);
            plan.HoldUntilAdvanced = true;
            sequence.Play(new string[0], plan);
            yield return null;
            Assert.That(sequence.IsPlaying, Is.True);
            if (Application.isBatchMode) yield return CaptureFraming("opening-title");
            Assert.That(sequence.IsPlaying, Is.True, "The title card is still up after the capture: it was photographed, not the house.");
            Assert.That(sequence.CurrentBeat, Is.EqualTo(OpeningBeat.Intro), "and it is still the title that is up.");
            sequence.Skip();
            yield return null;
            Assert.That(sequence.IsPlaying, Is.False);
        }

        /// <summary>
        /// The camera is taken for the sequence and given back afterwards. A player left unable to
        /// move the camera after the intro would have no way to work out why.
        /// </summary>
        [UnityTest]
        public IEnumerator Opening_TheCameraIsGivenBackWhenItIsOver()
        {
            var rig = Rig();
            Assert.That(rig, Is.Not.Null, "The scene should have a camera rig.");
            rig.ControlsEnabled = true;

            var sequence = Opening();
            var recorded = new List<string>();
            var plan = Plan(recorded);
            plan.Rig = rig;
            plan.WalkInKeys = Gamesim.Episode.EpisodeDirector.WalkInKeys;

            sequence.Play(new string[0], plan);
            yield return null;
            Assert.That(rig.ControlsEnabled, Is.False, "The sequence drives the camera.");

            yield return Until(sequence);
            Assert.That(rig.ControlsEnabled, Is.True);
            Assert.That(rig.HasShot, Is.False, "The walk-in's shot is given back with the camera, not left holding it.");
        }

        [UnityTest]
        public IEnumerator Opening_SkippingAlsoGivesTheCameraBack()
        {
            var rig = Rig();
            rig.ControlsEnabled = true;

            var sequence = Opening();
            var plan = Plan(new List<string>());
            plan.Rig = rig;
            sequence.Play(new string[0], plan);
            yield return null;

            sequence.Skip();
            yield return null;
            Assert.That(rig.ControlsEnabled, Is.True);
        }

        /// <summary>
        /// The skip control is a real button with the caption the tour and the tests look for, on
        /// every beat of the show rather than only the first — somebody who decides to skip during
        /// the walk-in should not have to wait for a beat that offers it. The introductions have
        /// their own skip instead.
        /// </summary>
        [UnityTest]
        public IEnumerator Opening_TheSkipControlIsReachableWhileItIsPlaying()
        {
            var sequence = Opening();
            sequence.Play(new string[0], Plan(new List<string>()));
            yield return null;

            var skip = sequence.GetComponentsInChildren<UnityEngine.UI.Button>(true)
                .Where(button => button.IsActive() && button.IsInteractable()
                    && button.GetComponentsInChildren<TMPro.TMP_Text>(true)
                        .Any(label => label.text == OpeningSequence.SkipCaption))
                .ToArray();

            Assert.That(skip, Has.Length.EqualTo(1));
            skip[0].onClick.Invoke();
            yield return null;
            Assert.That(sequence.IsPlaying, Is.False);
        }

        // ---------------------------------------------------------------- a real house, card by card

        /// <summary>The four beats of the show, which the introductions follow.</summary>
        private static readonly string[] SequenceShowBeats =
            { OpeningBeat.Intro, OpeningBeat.HouseEntry, OpeningBeat.WalkIn, OpeningBeat.Tutorial };

        /// <summary>
        /// The house these tests introduce: a built season of eight on seed 5 - the player, then
        /// Alex, Emma, Jordan, Casey, Riley, Jamie and Quinn in the roster's order - the same season
        /// the engine's own introduction tests use.
        /// </summary>
        private static EpisodeState SequenceSeason() =>
            SeasonBuilder.Create(new SeasonBuilder.Choice { HouseSize = 8 }, 5u);

        /// <summary>The seven houseguests the player meets, in the order they are met.</summary>
        private static List<ContestantState> SequenceGuests(EpisodeState season) =>
            season.Active.Where(person => !person.isPlayer).ToList();

        /// <summary>
        /// A plan for that house with every card held until the test moves it on and no movement, so
        /// what is on screen depends only on how many times the test has pressed Continue - in the
        /// editor and in batchmode alike.
        /// </summary>
        private static OpeningSequence.Settings SequencePlan(List<string> recorded, EpisodeState season,
            IEnumerable<ContestantState> cast = null)
        {
            var plan = Plan(recorded, reducedMotion: true);
            plan.HoldUntilAdvanced = true;
            plan.Cast = (cast ?? season.Active).ToList();
            plan.Seed = unchecked((int)season.seed);
            plan.ArrivalLine = season.events.Where(entry => entry.kind == "arrival")
                .Select(entry => entry.text).LastOrDefault() ?? string.Empty;
            return plan;
        }

        /// <summary>
        /// A stand-in for the director's introductions: it records each one as "id/approach" and
        /// judges it the way the engine does, so the card has a real outcome to react to.
        /// </summary>
        private static Func<string, string, OpeningSequence.Introduction> SequenceIntroductions(EpisodeState season, List<string> calls) =>
            (id, approach) =>
            {
                calls.Add(id + "/" + approach);
                var person = season.Find(id);
                var chosen = WebIntroductions.Find(approach);
                bool known = person != null && chosen != null;
                return new OpeningSequence.Introduction
                {
                    Accepted = known,
                    Reason = known ? null : "Introduce yourself to an active houseguest.",
                    Outcome = known ? WebIntroductions.Judge(chosen, person.traits) : WebIntroductions.Outcome.Neutral,
                };
            };

        /// <summary>
        /// The labels on screen under <paramref name="root"/>. Active ones only: each beat hides the
        /// last beat's pieces on the frame it clears them and destroys them after, so an inactive
        /// label belongs to a card that has already gone.
        /// </summary>
        private static TMP_Text[] SequenceLabels(Component root) =>
            root == null ? new TMP_Text[0] : root.GetComponentsInChildren<TMP_Text>(false);

        /// <summary>What the on-screen labels with this GameObject name say, in hierarchy order.</summary>
        private static string[] SequenceTexts(Component root, string name) =>
            SequenceLabels(root).Where(label => label.name == name).Select(label => label.text).ToArray();

        /// <summary>The newest active node with this name: the one on screen while a card is being replaced.</summary>
        private static Transform SequenceNode(Component root, string name) =>
            root == null ? null : root.GetComponentsInChildren<Transform>(false).LastOrDefault(node => node.name == name);

        /// <summary>The buttons on screen that carry a label reading exactly <paramref name="caption"/>.</summary>
        private static Button[] SequenceButtons(Component root, string caption) =>
            root.GetComponentsInChildren<Button>(false)
                .Where(button => button.GetComponentsInChildren<TMP_Text>(true).Any(label => label.text == caption))
                .ToArray();

        /// <summary>
        /// Frames until <paramref name="done"/> holds, bounded twice over: a batchmode frame is under
        /// half a millisecond, so a frame count alone is hardly any time, and a clock alone could spin
        /// for ever on a stalled frame.
        /// </summary>
        private static IEnumerator SequenceWait(Func<bool> done, float seconds = 5f, int frames = 50000)
        {
            float until = Time.realtimeSinceStartup + seconds;
            for (int frame = 0; frame < frames && !done() && Time.realtimeSinceStartup < until; frame++) yield return null;
        }

        /// <summary>
        /// Presses Continue once a frame until <paramref name="done"/> holds, the way a player taps
        /// through the cards, bounded the same way. The condition is read before every press, and a
        /// press left over when a card changes is dropped by the next card, so this never runs past
        /// the card it is looking for.
        /// </summary>
        private static IEnumerator SequenceStep(OpeningSequence sequence, Func<bool> done, float seconds = 5f, int frames = 50000)
        {
            float until = Time.realtimeSinceStartup + seconds;
            for (int frame = 0; frame < frames && !done() && sequence.IsPlaying && Time.realtimeSinceStartup < until; frame++)
            {
                sequence.Advance();
                yield return null;
            }
        }

        /// <summary>
        /// The front door without a house behind it. Every call is recorded, and every walk has
        /// arrived by the time it is asked about, so all that is under test is the sequence's own
        /// order of events. <see cref="Placeable"/> decides whether the house could be staged at all.
        /// </summary>
        private sealed class FakeStage : OpeningSequence.IStage
        {
            private readonly int housemates;
            private readonly HashSet<string> sentHome = new HashSet<string>();
            private bool restored;

            public readonly bool Placeable;
            public readonly List<string> Calls = new List<string>();

            /// <summary>Asked each time the house is put back: whether the screen hid it happening.</summary>
            public Func<bool> Opaque;
            public readonly List<bool> RestoredBehindOpaque = new List<bool>();

            public FakeStage(int housemates, bool placeable = true)
            {
                this.housemates = housemates;
                Placeable = placeable;
            }

            /// <summary>How many times <paramref name="call"/> was made, whoever it was for.</summary>
            public int Count(string call) => Calls.Count(entry => entry == call || entry.StartsWith(call + " ", StringComparison.Ordinal));

            public float BodyReadiness => 1f;
            public bool Placed { get; private set; }
            public bool Ready => true;
            public bool Failed => false;

            /// <summary>Home when nothing was ever placed, once put back, or once everybody has been sent home.</summary>
            public bool AllHome => !Placed || restored || sentHome.Count >= housemates;

            public House.HouseCameraRig.Shot DoorShot => new House.HouseCameraRig.Shot { Seconds = 0.6f };
            public House.HouseCameraRig.Shot PushInShot => new House.HouseCameraRig.Shot { Seconds = 0.5f };

            public bool TryPlace()
            {
                Calls.Add("TryPlace");
                Placed = Placeable;
                restored = false;
                sentHome.Clear();
                return Placeable;
            }

            public void RestoreHome()
            {
                Calls.Add("RestoreHome");
                RestoredBehindOpaque.Add(Opaque == null || Opaque());
                restored = true;
            }
            public bool OnDeck(string id) { Calls.Add("OnDeck " + id); return true; }
            public bool ToDoor(string id) { Calls.Add("ToDoor " + id); return true; }
            public bool AtDoor(string id) => true;
            public void OpenDoor() => Calls.Add("OpenDoor");
            public void CloseDoor() => Calls.Add("CloseDoor");
            public bool ThroughDoor(string id) { Calls.Add("ThroughDoor " + id); return true; }
            public bool OnMark(string id) => true;
            public void Present(string id) => Calls.Add("Present " + id);
            public void SendOff(string id) => Calls.Add("SendOff " + id);
            public void StrikeSet() => Calls.Add("StrikeSet");
            public void SendHome(string id) { Calls.Add("SendHome " + id); sentHome.Add(id); }
        }

        /// <summary>
        /// Two presses in one frame, either side of the sequence's own turn: one in Update, before
        /// coroutines resume, and one in LateUpdate, after them - the way Space and a submitted
        /// Continue can both report one keystroke. Two calls in a row from a test would not show
        /// anything, because the sequence keeps a single pending press and those collapse into it
        /// with or without the frame check.
        /// </summary>
        private sealed class DoublePress : MonoBehaviour
        {
            public OpeningSequence Sequence;
            public bool Done;
            private int frame = -1;

            private void Update()
            {
                if (Done || frame >= 0 || Sequence == null) return;
                Sequence.Advance();
                frame = Time.frameCount;
            }

            private void LateUpdate()
            {
                if (Done || frame != Time.frameCount) return;
                Sequence.Advance();
                Done = true;
            }
        }

        /// <summary>
        /// Every houseguest is revealed once, the player first and then the house in the season's
        /// order, whatever order the cast is handed over in. The reference build opens on the player
        /// because the show is about them; a player revealed eighth, after seven strangers, is
        /// watching somebody else's premiere.
        /// </summary>
        [UnityTest]
        public IEnumerator Opening_RevealsEveryHouseguestPlayerFirst()
        {
            var season = SequenceSeason();
            var guests = SequenceGuests(season);
            var playerLast = guests.Concat(season.Active.Where(person => person.isPlayer)).ToList();
            Assert.That(playerLast, Has.Count.EqualTo(8));
            Assert.That(playerLast.Last().isPlayer, Is.True, "The cast is handed over with the player last.");

            var sequence = Opening();
            sequence.Play(new string[0], SequencePlan(new List<string>(), season, playerLast));
            yield return null;
            Assert.That(sequence.CurrentBeat, Is.EqualTo(OpeningBeat.Intro));
            Assert.That(sequence.CurrentGuestId, Is.Null, "The title card comes before anybody.");

            var revealed = new List<string>();
            float until = Time.realtimeSinceStartup + 5f;
            for (int frame = 0; frame < 50000 && sequence.CurrentBeat == OpeningBeat.Intro && Time.realtimeSinceStartup < until; frame++)
            {
                var id = sequence.CurrentGuestId;
                if (id != null && (revealed.Count == 0 || revealed[revealed.Count - 1] != id)) revealed.Add(id);
                sequence.Advance();
                yield return null;
            }

            Assert.That(sequence.CurrentBeat, Is.EqualTo(OpeningBeat.HouseEntry), "Continue takes the intro through to the house entry.");
            CollectionAssert.AreEqual(new[] { season.playerId }.Concat(guests.Select(person => person.id)).ToList(), revealed,
                "The player is revealed first, then everybody else once each, in the order they were given.");
            sequence.Skip();
            yield return null;
        }

        /// <summary>
        /// Each reveal says who the houseguest is: their name, their age, what they do, where they
        /// are from, and which of the house they are. A line the houseguest has nothing for is left
        /// out rather than drawn empty - the default player has no hometown, and a blank third line
        /// under them reads as a rendering fault.
        /// </summary>
        [UnityTest]
        public IEnumerator Opening_TheLowerThirdSaysWhoTheyAre()
        {
            var season = SequenceSeason();
            var sequence = Opening();
            sequence.Play(new string[0], SequencePlan(new List<string>(), season));
            yield return SequenceStep(sequence, () => sequence.CurrentGuestId == season.playerId);
            Assert.That(sequence.CurrentGuestId, Is.EqualTo(season.playerId));

            var third = SequenceNode(sequence, "Lower third");
            Assert.That(third, Is.Not.Null, "Each reveal names its houseguest in a lower third.");
            CollectionAssert.AreEqual(new[] { "You" }, SequenceTexts(third, "Name"));
            var lines = SequenceTexts(third, "Line");
            CollectionAssert.AreEqual(new[] { "Age 30", "Houseguest" }, lines,
                "The default player has an age and an occupation but no hometown.");
            Assert.That(lines.Where(string.IsNullOrWhiteSpace), Is.Empty, "No line of the lower third is drawn empty.");
            CollectionAssert.AreEqual(new[] { "1 of 8" }, SequenceTexts(third, "Counter"));

            yield return SequenceStep(sequence, () => sequence.CurrentGuestId == "alex-chen");
            Assert.That(sequence.CurrentGuestId, Is.EqualTo("alex-chen"));
            third = SequenceNode(sequence, "Lower third");
            Assert.That(third, Is.Not.Null);
            CollectionAssert.AreEqual(new[] { "Alex Chen" }, SequenceTexts(third, "Name"));
            CollectionAssert.AreEqual(new[] { "Age 28", "Marketing Executive", "San Francisco, CA" }, SequenceTexts(third, "Line"));
            CollectionAssert.AreEqual(new[] { "2 of 8" }, SequenceTexts(third, "Counter"), "The count is the houseguest's place, from one.");

            sequence.Skip();
            yield return null;
        }

        /// <summary>
        /// The frame itself is the Continue control, and pressing it moves on to the next
        /// houseguest - one, not the rest of the show. A card that ignored the click would leave a
        /// player clicking at a still frame with nothing to tell them what the show is waiting for.
        /// </summary>
        [UnityTest]
        public IEnumerator Opening_ContinueMovesToTheNextHouseguest()
        {
            var season = SequenceSeason();
            var sequence = Opening();
            sequence.Play(new string[0], SequencePlan(new List<string>(), season));
            yield return SequenceStep(sequence, () => sequence.CurrentGuestId == season.playerId);

            for (int frame = 0; frame < 3; frame++) yield return null;
            Assert.That(sequence.CurrentGuestId, Is.EqualTo(season.playerId), "Left alone, a held card stays up.");

            var node = SequenceNode(sequence, OpeningSequence.ContinueCaption);
            var button = node != null ? node.GetComponent<Button>() : null;
            Assert.That(button, Is.Not.Null, "Every card of the show is a Continue control from edge to edge.");
            button.onClick.Invoke();
            for (int frame = 0; frame < 3 && sequence.CurrentGuestId == season.playerId; frame++) yield return null;
            Assert.That(sequence.CurrentGuestId, Is.EqualTo("alex-chen"), "Continue moves on to the next houseguest, and only the next.");

            sequence.Skip();
            yield return null;
        }

        /// <summary>
        /// Continue counts once a frame. A keystroke can arrive twice - Space is read directly, and
        /// the same press can submit the focused Continue - and a press counted twice skips a
        /// houseguest the player never saw.
        /// </summary>
        [UnityTest]
        public IEnumerator Opening_AdvanceIsCountedOncePerFrame()
        {
            var season = SequenceSeason();
            var sequence = Opening();
            sequence.Play(new string[0], SequencePlan(new List<string>(), season));
            yield return SequenceStep(sequence, () => sequence.CurrentGuestId == season.playerId);
            yield return null;

            sequence.Advance();
            sequence.Advance();
            for (int frame = 0; frame < 5; frame++) yield return null;
            Assert.That(sequence.CurrentGuestId, Is.EqualTo("alex-chen"), "Two presses back to back move one card.");

            var presser = new GameObject("Opening double press").AddComponent<DoublePress>();
            presser.Sequence = sequence;
            yield return SequenceWait(() => presser.Done, 2f, 200);
            Assert.That(presser.Done, Is.True, "Both presses were made in one frame.");
            for (int frame = 0; frame < 5; frame++) yield return null;
            UnityEngine.Object.Destroy(presser.gameObject);
            Assert.That(sequence.CurrentGuestId, Is.EqualTo("emma-brown"),
                "A press either side of the sequence's turn in one frame still moves one card.");

            sequence.Skip();
            yield return null;
        }

        /// <summary>
        /// The title card says what the season is: the wordmark, how many are playing for how many
        /// wins, and the eye over it all. It is the frame the whole opening is judged by, and the
        /// count is the one fact on it that changes from season to season.
        /// </summary>
        [UnityTest]
        public IEnumerator Opening_TheTitleSaysHowManyAndWhoWins()
        {
            var season = SequenceSeason();
            var sequence = Opening();
            sequence.Play(new string[0], SequencePlan(new List<string>(), season));
            yield return null;
            Assert.That(sequence.CurrentBeat, Is.EqualTo(OpeningBeat.Intro));
            Assert.That(sequence.CurrentGuestId, Is.Null, "The title is up, not a houseguest.");

            var texts = SequenceLabels(sequence).Select(label => label.text).ToArray();
            Assert.That(texts, Does.Contain("GAMESIM"));
            Assert.That(texts, Does.Contain("8 Houseguests. 1 Winner."), "The title counts the house it opens.");
            Assert.That(SequenceNode(sequence, "Eye emblem"), Is.Not.Null, "The eye stands over the title.");

            sequence.Skip();
            yield return null;
        }

        /// <summary>
        /// The house-entry card is the reference build's - "Welcome to the House", and the house
        /// counted in under it - and the season's own opening line is read out with it. The
        /// reference's "...competition starting soon" is gone: three beats follow this card now, and
        /// promising a competition that is several minutes away is a promise the show breaks.
        /// </summary>
        [UnityTest]
        public IEnumerator Opening_TheHouseEntryCardReadsTheSeasonsArrivalLine()
        {
            var season = SequenceSeason();
            var plan = SequencePlan(new List<string>(), season);
            Assert.That(plan.ArrivalLine, Is.Not.Empty, "A built season records its own opening line.");

            var sequence = Opening();
            sequence.Play(new[] { OpeningBeat.Intro }, plan);
            yield return SequenceWait(() => sequence.CurrentBeat == OpeningBeat.HouseEntry, 2f, 200);
            Assert.That(sequence.CurrentBeat, Is.EqualTo(OpeningBeat.HouseEntry));

            var texts = SequenceLabels(sequence).Select(label => label.text ?? string.Empty).ToArray();
            Assert.That(texts, Does.Contain("Welcome to the House"), "The reference's welcome heads the card.");
            Assert.That(texts, Does.Contain("8 Houseguests have entered"));
            Assert.That(texts, Does.Contain(plan.ArrivalLine), "The season's arrival line is read out word for word.");
            Assert.That(texts.Where(text => text.IndexOf("starting soon", StringComparison.OrdinalIgnoreCase) >= 0), Is.Empty,
                "Nothing promises a competition that is three beats away.");

            sequence.Skip();
            yield return null;
        }

        /// <summary>
        /// The introductions ask the reference build's question with everything the player needs to
        /// answer it: who this is, every trait they have, what they say about themselves, and the
        /// three ways to introduce yourself, each captioned with its name alone. They have their own
        /// skip, and the show's skip is not on this card, because skipping here forfeits a choice
        /// rather than a cinematic.
        /// </summary>
        [UnityTest]
        public IEnumerator Opening_TheMeetAsksHowYouIntroduceYourself()
        {
            var season = SequenceSeason();
            var alex = season.Find("alex-chen");
            var plan = SequencePlan(new List<string>(), season);
            plan.Introduce = SequenceIntroductions(season, new List<string>());
            var sequence = Opening();
            sequence.Play(SequenceShowBeats, plan);
            yield return SequenceWait(() => sequence.IsMeeting && sequence.CurrentGuestId == alex.id, 2f, 200);
            Assert.That(sequence.CurrentBeat, Is.EqualTo(OpeningBeat.MeetAndGreet));
            Assert.That(sequence.IsMeeting, Is.True);
            Assert.That(sequence.CurrentGuestId, Is.EqualTo(alex.id), "The first houseguest in the season's order is met first.");

            Assert.That(SequenceTexts(sequence, "Heading"), Does.Contain(OpeningSequence.MeetHeading));
            CollectionAssert.AreEqual(new[] { "1 of 7" }, SequenceTexts(sequence, "Count"), "The count is of the houseguests to meet.");

            var card = SequenceNode(sequence, "Card");
            Assert.That(card, Is.Not.Null, "Each houseguest has a card.");
            CollectionAssert.AreEqual(new[] { alex.name }, SequenceTexts(card, "Name"));
            var chips = SequenceLabels(card)
                .Where(label => label.name == "Word" && label.transform.parent != null && label.transform.parent.name == "Trait")
                .Select(label => label.text).ToList();
            CollectionAssert.AreEqual(alex.traits, chips, "Every trait is on the card: reading them is how the player chooses.");
            CollectionAssert.AreEqual(
                new[] { "\"" + WebIntroductions.IntroLine(plan.Seed, alex.id, alex.name, alex.traits) + "\"" },
                SequenceTexts(card, "Quote"), "The houseguest's own line, in straight quote marks as the reference build prints it.");
            CollectionAssert.AreEqual(new[] { WebIntroductions.Prompt }, SequenceTexts(card, "Prompt"));

            var slot = SequenceNode(card, "Choices");
            Assert.That(slot, Is.Not.Null);
            var choices = slot.GetComponentsInChildren<Button>(false);
            CollectionAssert.AreEqual(new[] { "Warm", "Calculated", "Bold" }, choices.Select(choice => choice.name).ToList());
            CollectionAssert.AreEqual(new[] { "Warm", "Calculated", "Bold" },
                choices.Select(choice => choice.GetComponentsInChildren<TMP_Text>(true).Single(label => label.name == "Label").text).ToList(),
                "Each choice's caption is the approach's name alone.");
            CollectionAssert.AreEqual(WebIntroductions.Approaches.Select(approach => approach.Line).ToList(),
                choices.Select(choice => choice.GetComponentsInChildren<TMP_Text>(true).Single(label => label.name == "Sublabel").text).ToList(),
                "What the player says goes in a label of its own beside the caption.");

            Assert.That(SequenceButtons(sequence, OpeningSequence.SkipIntroductionsCaption), Has.Length.EqualTo(1),
                "The introductions have exactly one skip of their own.");
            Assert.That(SequenceButtons(sequence, OpeningSequence.SkipCaption).Where(button => button.IsActive()), Is.Empty,
                "The show's skip is not offered over a choice.");

            sequence.Skip();
            yield return null;
        }

        /// <summary>
        /// An answer is committed once however often it is pressed, and what comes back is the
        /// houseguest's reply in their own words - never the number or the verdict, which the
        /// reference build's meet never shows either. The keyboard then lands on Next: left to
        /// itself it falls through to "Skip Introductions", and the next Enter forfeits the house.
        /// </summary>
        [UnityTest]
        public IEnumerator Opening_AnAnswerShowsTheReactionNotTheNumber()
        {
            var season = SequenceSeason();
            var alex = season.Find("alex-chen");
            var calls = new List<string>();
            var plan = SequencePlan(new List<string>(), season);
            plan.Introduce = SequenceIntroductions(season, calls);
            var sequence = Opening();
            sequence.Play(SequenceShowBeats, plan);
            yield return SequenceWait(() => sequence.IsMeeting && sequence.CurrentGuestId == alex.id, 2f, 200);
            Assert.That(sequence.CurrentGuestId, Is.EqualTo(alex.id));
            // A press on the frame the card was built is ignored: it belongs to the last card.
            yield return null;

            var choice = SequenceNode(sequence, "Calculated");
            Assert.That(choice, Is.Not.Null, "The card offers Calculated.");
            var press = choice.GetComponent<Button>().onClick;
            press.Invoke();
            yield return null;
            press.Invoke();

            CollectionAssert.AreEqual(new[] { alex.id + "/" + WebIntroductions.Calculated }, calls,
                "The introduction is committed once, however many times it is pressed.");
            string reaction = WebIntroductions.Reaction(plan.Seed, alex.id, alex.name, alex.traits,
                WebIntroductions.Find(WebIntroductions.Calculated));
            Assert.That(SequenceLabels(sequence).Select(label => label.text), Does.Contain(alex.name + ": \"" + reaction + "\""),
                "The houseguest answers in their own words.");
            var everything = sequence.GetComponentsInChildren<TMP_Text>(true).Select(label => label.text ?? string.Empty).ToArray();
            Assert.That(everything.Where(text => text.Contains("+3")), Is.Empty, "No number says how it went.");
            Assert.That(everything.Where(text => text.IndexOf("match", StringComparison.OrdinalIgnoreCase) >= 0
                || text.IndexOf("clash", StringComparison.OrdinalIgnoreCase) >= 0), Is.Empty, "No verdict says how it went.");

            var selected = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
            Assert.That(selected, Is.Not.Null, "Something has the keyboard after an answer.");
            Assert.That(selected.name, Is.EqualTo(OpeningSequence.NextCaption),
                "The keyboard is on Next after an answer, not on Skip Introductions.");

            sequence.Skip();
            yield return null;
        }

        /// <summary>
        /// The last houseguest's onward button is "Let's Play!", and pressing it ends the opening
        /// with the introductions recorded. Recorded last and only then: a season reloaded halfway
        /// through the house must come back to the introductions, not past them.
        /// </summary>
        [UnityTest]
        public IEnumerator Opening_TheLastHouseguestSaysLetsPlayAndEndsTheOpening()
        {
            var season = SequenceSeason();
            var guests = SequenceGuests(season);
            var calls = new List<string>();
            var recorded = new List<string>();
            bool finished = false;
            var plan = SequencePlan(recorded, season);
            plan.Introduce = SequenceIntroductions(season, calls);
            plan.Finished = () => finished = true;
            var sequence = Opening();
            sequence.Play(SequenceShowBeats, plan);

            for (int i = 0; i < guests.Count; i++)
            {
                var guest = guests[i];
                yield return SequenceWait(() => sequence.IsMeeting && sequence.CurrentGuestId == guest.id, 2f, 200);
                Assert.That(sequence.CurrentGuestId, Is.EqualTo(guest.id), "Card " + (i + 1) + " is " + guest.name + ".");
                yield return null;

                var warm = SequenceNode(sequence, "Warm");
                Assert.That(warm, Is.Not.Null, guest.name + "'s card offers Warm.");
                warm.GetComponent<Button>().onClick.Invoke();
                yield return null;

                bool last = i == guests.Count - 1;
                var onward = SequenceNode(sequence, last ? OpeningSequence.LetsPlayCaption : OpeningSequence.NextCaption);
                Assert.That(onward, Is.Not.Null, last ? "The last houseguest ends with Let's Play!" : guest.name + " is followed by Next.");
                Assert.That(SequenceNode(sequence, last ? OpeningSequence.NextCaption : OpeningSequence.LetsPlayCaption), Is.Null,
                    "One onward button per card.");
                Assert.That(recorded, Is.Empty, "Nothing is recorded until the last houseguest has been met.");
                onward.GetComponent<Button>().onClick.Invoke();
            }
            yield return SequenceWait(() => !sequence.IsPlaying, 2f, 200);

            Assert.That(sequence.IsPlaying, Is.False, "Let's Play! ends the opening.");
            Assert.That(finished, Is.True, "The caller is told it is over.");
            Assert.That(calls, Has.Count.EqualTo(guests.Count), "Everybody was introduced to, once.");
            CollectionAssert.AreEqual(new[] { OpeningBeat.MeetAndGreet }, recorded);
        }

        /// <summary>
        /// "Skip Introductions" ends the introductions at once, and whoever was not met gets nothing
        /// - no impression either way, as in the reference build. They can still be talked to
        /// later, through the week's ordinary conversations.
        /// </summary>
        [UnityTest]
        public IEnumerator Opening_SkipIntroductionsForfeitsTheRest()
        {
            var season = SequenceSeason();
            var alex = season.Find("alex-chen");
            var calls = new List<string>();
            var recorded = new List<string>();
            var plan = SequencePlan(recorded, season);
            plan.Introduce = SequenceIntroductions(season, calls);
            var sequence = Opening();
            sequence.Play(SequenceShowBeats, plan);
            yield return SequenceWait(() => sequence.IsMeeting && sequence.CurrentGuestId == alex.id, 2f, 200);
            yield return null;

            var warm = SequenceNode(sequence, "Warm");
            Assert.That(warm, Is.Not.Null);
            warm.GetComponent<Button>().onClick.Invoke();
            yield return null;

            var skip = SequenceButtons(sequence, OpeningSequence.SkipIntroductionsCaption);
            Assert.That(skip, Has.Length.EqualTo(1), "Skip Introductions is still on offer after an answer.");
            skip[0].onClick.Invoke();
            yield return SequenceWait(() => !sequence.IsPlaying, 2f, 200);

            Assert.That(sequence.IsPlaying, Is.False, "Skip Introductions ends them at once.");
            CollectionAssert.AreEqual(new[] { alex.id + "/" + WebIntroductions.Warm }, calls,
                "Only the houseguest actually met was introduced to.");
            CollectionAssert.AreEqual(new[] { OpeningBeat.MeetAndGreet }, recorded, "The introductions are over, and recorded as over.");
        }

        /// <summary>
        /// The show's skip stops at the introductions: the four beats of the show are recorded as
        /// skipped, and the first card is put up. Skipping a title card must never silently forfeit
        /// a choice the player has not been shown yet.
        /// </summary>
        [UnityTest]
        public IEnumerator Opening_TheSkipPillStopsAtTheIntroductions()
        {
            var season = SequenceSeason();
            var calls = new List<string>();
            var recorded = new List<string>();
            var plan = SequencePlan(recorded, season);
            plan.Introduce = SequenceIntroductions(season, calls);
            var sequence = Opening();
            sequence.Play(new string[0], plan);
            yield return null;
            Assert.That(sequence.CurrentBeat, Is.EqualTo(OpeningBeat.Intro));
            Assert.That(sequence.IntroductionsPending, Is.True, "There are seven houseguests to meet.");

            var pill = SequenceButtons(sequence, OpeningSequence.SkipCaption).Where(button => button.IsActive()).ToArray();
            Assert.That(pill, Has.Length.EqualTo(1));
            pill[0].onClick.Invoke();
            yield return SequenceWait(() => sequence.IsMeeting, 2f, 200);

            CollectionAssert.AreEqual(SequenceShowBeats, recorded, "The show is recorded as skipped, in order.");
            Assert.That(sequence.IsPlaying, Is.True, "The introductions are still to come.");
            Assert.That(sequence.CurrentBeat, Is.EqualTo(OpeningBeat.MeetAndGreet));
            Assert.That(sequence.IsMeeting, Is.True, "The first card is up.");
            Assert.That(calls, Is.Empty, "Skipping the show introduced nobody.");

            sequence.SkipIntroductions();
            yield return SequenceWait(() => !sequence.IsPlaying, 2f, 200);
            Assert.That(sequence.IsPlaying, Is.False);
            CollectionAssert.AreEqual(OpeningBeat.InOrder, recorded);
        }

        /// <summary>
        /// A season reloaded partway through the introductions picks up at the first houseguest the
        /// player has not met, counted in the whole house. Starting again at the first card would
        /// offer the player a second first impression of somebody the engine will refuse.
        /// </summary>
        [UnityTest]
        public IEnumerator Opening_AResumedMeetStartsWithTheFirstStranger()
        {
            var season = SequenceSeason();
            var guests = SequenceGuests(season);
            var met = new HashSet<string> { guests[0].id, guests[1].id };
            var framed = new List<string>();
            var plan = SequencePlan(new List<string>(), season);
            plan.Introduce = SequenceIntroductions(season, new List<string>());
            plan.Introduced = met.Contains;
            plan.FrameGuest = framed.Add;
            var sequence = Opening();
            sequence.Play(SequenceShowBeats, plan);
            yield return SequenceWait(() => sequence.IsMeeting, 2f, 200);

            Assert.That(sequence.IsMeeting, Is.True);
            Assert.That(sequence.CurrentGuestId, Is.EqualTo(guests[2].id), "The first houseguest not yet met comes first.");
            CollectionAssert.AreEqual(new[] { "3 of 7" }, SequenceTexts(sequence, "Count"), "Counted in the whole house, not from one.");
            CollectionAssert.AreEqual(new[] { guests[2].name }, SequenceTexts(SequenceNode(sequence, "Card"), "Name"));
            CollectionAssert.AreEqual(new[] { guests[2].id }, framed, "The camera goes straight to them.");

            sequence.Skip();
            yield return null;
        }

        /// <summary>
        /// A house that cannot be staged at the front door is revealed on cards instead - the
        /// reference build's own fallback for a houseguest with no body - and nobody is walked
        /// towards a door that was never built.
        /// </summary>
        [UnityTest]
        public IEnumerator Opening_AStageThatCannotPlaceRevealsInTwoD()
        {
            var season = SequenceSeason();
            var stage = new FakeStage(season.Active.Count(), placeable: false);
            var plan = SequencePlan(new List<string>(), season);
            plan.ReducedMotion = false;
            // Headless runs skip the movement, and with it the stage; this one asks for both.
            plan.MotionInBatchmode = true;
            plan.Stage = stage;
            var sequence = Opening();
            sequence.Play(new string[0], plan);
            yield return SequenceStep(sequence, () => sequence.CurrentGuestId == season.playerId);

            Assert.That(sequence.CurrentGuestId, Is.EqualTo(season.playerId));
            Assert.That(stage.Count("TryPlace"), Is.EqualTo(1), "The house was offered the front door, once.");
            Assert.That(stage.Placed, Is.False);
            var ground = SequenceNode(sequence, "Stage");
            Assert.That(ground, Is.Not.Null);
            Assert.That(ground.Cast<Transform>().Any(child => child.name == "Portrait" && child.gameObject.activeInHierarchy), Is.True,
                "The houseguest is revealed as a portrait on a card.");

            yield return SequenceStep(sequence, () => sequence.CurrentBeat != OpeningBeat.Intro);
            Assert.That(sequence.CurrentBeat, Is.EqualTo(OpeningBeat.HouseEntry));
            Assert.That(stage.Count("ToDoor"), Is.Zero, "Nobody is walked to a door that was never built.");
            Assert.That(stage.Count("ThroughDoor"), Is.Zero);
            Assert.That(stage.Count("OpenDoor"), Is.Zero);

            sequence.Skip();
            yield return null;
        }

        /// <summary>
        /// Skipping in the middle of the door reveals takes the front door down and puts the house
        /// back where the season started it - once, and behind black - before the introductions
        /// play. A house left scattered behind a door that is no longer there is the one outcome of
        /// a skip that nobody would ever recover from on their own.
        /// </summary>
        [UnityTest]
        public IEnumerator Opening_SkippingMidRevealRestoresTheHouseOnce()
        {
            var season = SequenceSeason();
            var order = season.Active.ToList();
            var stage = new FakeStage(order.Count);
            var calls = new List<string>();
            var recorded = new List<string>();
            var plan = SequencePlan(recorded, season);
            plan.ReducedMotion = false;
            // Headless runs skip the movement, and with it the stage; this one asks for both.
            plan.MotionInBatchmode = true;
            plan.Stage = stage;
            plan.Introduce = SequenceIntroductions(season, calls);
            var sequence = Opening();
            stage.Opaque = () => sequence.GetComponent<CanvasGroup>().alpha >= 0.99f
                && sequence.GetComponentsInChildren<Image>(false).Any(image => image.name == "Scrim" && image.enabled && image.color.a >= 0.99f);
            sequence.Play(new string[0], plan);

            // The half-beat between the door opening and the walk through it is real time Continue
            // does not shorten, hence the longer bound.
            var second = order[1].id;
            yield return SequenceStep(sequence, () => sequence.CurrentGuestId == second, 10f);
            Assert.That(sequence.CurrentGuestId, Is.EqualTo(second), "The second houseguest is at the door.");
            Assert.That(stage.Placed, Is.True, "The house was placed behind the front door.");
            Assert.That(stage.Calls, Does.Contain("ToDoor " + season.playerId), "The player walked up to the door first.");
            Assert.That(stage.Calls, Does.Contain("Present " + season.playerId));
            Assert.That(stage.Calls, Does.Contain("SendOff " + season.playerId));
            Assert.That(stage.Count("RestoreHome"), Is.Zero, "Nobody is put back while the reveals play.");
            Assert.That(stage.Count("StrikeSet"), Is.Zero, "The door stays up while the reveals play.");

            var pill = SequenceButtons(sequence, OpeningSequence.SkipCaption).Where(button => button.IsActive()).ToArray();
            Assert.That(pill, Has.Length.EqualTo(1), "The show's skip is on the door reveals.");
            pill[0].onClick.Invoke();
            yield return SequenceWait(() => sequence.IsMeeting, 10f);

            Assert.That(sequence.IsMeeting, Is.True, "The introductions play after the skip.");
            Assert.That(stage.Count("StrikeSet"), Is.EqualTo(1), "The front door comes down.");
            Assert.That(stage.Count("RestoreHome"), Is.EqualTo(1), "The house is put back where the season started it, once.");
            CollectionAssert.AreEqual(new[] { true }, stage.RestoredBehindOpaque, "Nobody is seen jumping home: it happens behind black.");
            CollectionAssert.AreEqual(SequenceShowBeats, recorded);
            Assert.That(calls, Is.Empty);

            sequence.SkipIntroductions();
            yield return SequenceWait(() => !sequence.IsPlaying, 2f, 2000);
            Assert.That(sequence.IsPlaying, Is.False);
            Assert.That(stage.Count("RestoreHome"), Is.EqualTo(1), "Ending the introductions does not put the house back a second time.");
            Assert.That(sequence.WasSkipped, Is.True, "The caller is told the show was skipped.");
            CollectionAssert.AreEqual(OpeningBeat.InOrder, recorded);
        }

        /// <summary>
        /// Skipping while the tour is up closes the tour as well as the opening, and records all
        /// five beats. Continue does not close the tour - it has its own Next, and a click meant
        /// for the show must not throw away a page of it. A tour left open over a finished
        /// opening would be pointing at a HUD that has moved on.
        /// </summary>
        [UnityTest]
        public IEnumerator Opening_SkipDuringTheTourEndsTheTour()
        {
            var recorded = new List<string>();
            int toursStarted = 0, toursCancelled = 0;
            var plan = Plan(recorded, reducedMotion: true);
            plan.HoldUntilAdvanced = true;
            // A tour the player is still reading: it never calls back.
            plan.RunTutorial = done => toursStarted++;
            plan.CancelTutorial = () => toursCancelled++;
            var sequence = Opening();
            sequence.Play(new string[0], plan);
            yield return SequenceStep(sequence, () => sequence.CurrentBeat == OpeningBeat.Tutorial);

            Assert.That(sequence.CurrentBeat, Is.EqualTo(OpeningBeat.Tutorial));
            Assert.That(toursStarted, Is.EqualTo(1));
            CollectionAssert.AreEqual(new[] { OpeningBeat.Intro, OpeningBeat.HouseEntry, OpeningBeat.WalkIn }, recorded);

            for (int frame = 0; frame < 5; frame++) { sequence.Advance(); yield return null; }
            Assert.That(sequence.CurrentBeat, Is.EqualTo(OpeningBeat.Tutorial), "Continue does not close the tour.");
            Assert.That(recorded, Does.Not.Contain(OpeningBeat.Tutorial));
            Assert.That(toursCancelled, Is.Zero);

            sequence.Skip();
            yield return null;
            Assert.That(toursCancelled, Is.EqualTo(1), "Skipping closes the tour.");
            Assert.That(sequence.IsPlaying, Is.False);
            CollectionAssert.AreEqual(OpeningBeat.InOrder, recorded);
        }

        /// <summary>
        /// Skipping the show from under the tour still puts the introductions on screen. The tour
        /// hides the sequence's canvas while it points at the HUD; a skip that ends the tour early
        /// must bring the canvas back, or the introductions wait on a card nobody can see or click.
        /// </summary>
        [UnityTest]
        public IEnumerator Opening_SkippingTheShowFromTheTourShowsTheIntroductions()
        {
            var season = SequenceSeason();
            var recorded = new List<string>();
            int toursCancelled = 0;
            var plan = SequencePlan(recorded, season);
            plan.Introduce = SequenceIntroductions(season, new List<string>());
            plan.RunTutorial = done => { };
            plan.CancelTutorial = () => toursCancelled++;
            var sequence = Opening();
            sequence.Play(new[] { OpeningBeat.Intro, OpeningBeat.HouseEntry, OpeningBeat.WalkIn }, plan);
            yield return SequenceWait(() => sequence.CurrentBeat == OpeningBeat.Tutorial, 2f, 200);
            Assert.That(sequence.CurrentBeat, Is.EqualTo(OpeningBeat.Tutorial));

            sequence.PressSkip();
            yield return SequenceWait(() => sequence.IsMeeting, 2f, 200);
            Assert.That(toursCancelled, Is.EqualTo(1), "The tour is closed.");
            Assert.That(sequence.IsMeeting, Is.True, "The introductions follow.");
            var canvas = sequence.GetComponent<CanvasGroup>();
            Assert.That(canvas.alpha, Is.EqualTo(1f), "The introductions are on screen, not behind the tour's hidden canvas.");
            Assert.That(canvas.blocksRaycasts, Is.True, "The introductions can be clicked.");

            sequence.Skip();
            yield return null;
        }

        /// <summary>
        /// The caller is told each beat as it starts, in order, while it is the beat on screen and
        /// before it is recorded. The director hangs the music and the HUD on this - the theme
        /// under the intro only, the HUD back for the tour - and a beat announced late plays its
        /// first frame with the last beat's music and chrome.
        /// </summary>
        [UnityTest]
        public IEnumerator Opening_TellsTheCallerEachBeatAsItStarts()
        {
            var recorded = new List<string>();
            var started = new List<string>();
            var onScreen = new List<string>();
            var recordedEarly = new List<string>();
            var sequence = Opening();
            var plan = Plan(recorded, reducedMotion: true);
            plan.HoldUntilAdvanced = true;
            plan.BeatStarted = beat =>
            {
                started.Add(beat);
                onScreen.Add(sequence.CurrentBeat);
                if (recorded.Contains(beat)) recordedEarly.Add(beat);
            };
            sequence.Play(new string[0], plan);
            yield return SequenceStep(sequence, () => !sequence.IsPlaying);

            Assert.That(sequence.IsPlaying, Is.False);
            CollectionAssert.AreEqual(OpeningBeat.InOrder, started, "Every beat is announced, once, in order.");
            CollectionAssert.AreEqual(OpeningBeat.InOrder, onScreen, "Each is the beat on screen as it is announced.");
            Assert.That(recordedEarly, Is.Empty, "Each is announced before it is recorded.");
            CollectionAssert.AreEqual(OpeningBeat.InOrder, recorded);
        }
    }
}
