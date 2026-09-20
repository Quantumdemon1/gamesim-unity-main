using System.Collections;
using System.IO;
using System.Linq;
using Gamesim.Presentation;
using Gamesim.Simulation;
using NUnit.Framework;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;

namespace Gamesim.Tests.PlayMode
{
    /// <summary>
    /// The character creator, driven through its own controls.
    ///
    /// <para>The same standard as the cast screen it opens from: nothing is written until the player
    /// says so, and backing out has to land on the screen they came from with their roster, filter
    /// and pick still on it. Losing those to a trip through the creator would be a small failure that
    /// feels like a large one.</para>
    /// </summary>
    public sealed partial class EpisodePlayModeTests
    {
        private CharacterCreator Creator() => director.GetComponentInChildren<CharacterCreator>(true);

        [UnityTest]
        public IEnumerator Creator_TheDirectorSharesAnIsolatedLibraryWithCastSelection()
        {
            var creator = Creator();
            var cast = CastScreen();
            Assert.That(creator.ProfileStore, Is.SameAs(cast.ProfileStore));
            Assert.That(creator.ProfileStore.DirectoryPath,
                Is.EqualTo(Path.GetFullPath(Path.Combine(temporaryDirectory, "Houseguests"))));
            yield return null;
        }

        private IEnumerator OpenCreator()
        {
            yield return OpenCastScreen();
            var open = CastButtons(CharacterCreator.CreateCaption);
            Assert.That(open, Has.Length.EqualTo(1), "The cast screen should offer the creator once.");
            open[0].onClick.Invoke();
            yield return null;
            yield return null;
        }

        [UnityTest]
        public IEnumerator Creator_OpeningTheFormWritesNothing()
        {
            var before = director.Snapshot;
            string slot = director.SavePath;
            var bytes = File.Exists(slot) ? File.ReadAllBytes(slot) : null;

            yield return OpenCreator();

            Assert.That(Creator(), Is.Not.Null, "The director must attach a creator.");
            Assert.That(Creator().IsShowing, Is.True);
            Assert.That(CastScreen().IsShowing, Is.False, "The cast screen steps aside rather than stacking.");
            Assert.That(director.SavePath, Is.EqualTo(slot));
            Assert.That(director.Snapshot.sessionId, Is.EqualTo(before.sessionId));
            if (bytes != null) Assert.That(File.ReadAllBytes(slot), Is.EqualTo(bytes));
        }

        [UnityTest]
        public IEnumerator Creator_GoingBackReturnsToTheCastScreen()
        {
            var before = director.Snapshot;
            yield return OpenCreator();

            var back = CastButtons(CharacterCreator.BackCaption);
            Assert.That(back, Has.Length.EqualTo(1));
            back[0].onClick.Invoke();
            yield return null;
            yield return null;

            Assert.That(Creator().IsShowing, Is.False);
            Assert.That(CastScreen().IsShowing, Is.True,
                "Backing out lands on the grid, not on the house with nothing to go back to.");
            Assert.That(director.Snapshot.sessionId, Is.EqualTo(before.sessionId));
        }

        /// <summary>
        /// Escape reaches the form on top rather than the screen underneath it. The creator draws
        /// above the cast screen, so the cast screen closing first would pull the ground out.
        /// </summary>
        [UnityTest]
        public IEnumerator Creator_EscapeClosesTheFormAndNotTheScreenUnderneath()
        {
            yield return OpenCreator();

            testKeyboard = InputSystem.AddDevice<Keyboard>();
            InputSystem.QueueStateEvent(testKeyboard, new KeyboardState(Key.Escape));
            yield return null;
            InputSystem.QueueStateEvent(testKeyboard, new KeyboardState());
            yield return null; yield return null;

            Assert.That(Creator().IsShowing, Is.False);
            Assert.That(CastScreen().IsShowing, Is.True);
        }

        /// <summary>
        /// A nameless houseguest cannot start a season. The control is drawn either way — a button
        /// that vanishes is harder to find again than one that says why it will not go.
        /// </summary>
        [UnityTest]
        public IEnumerator Creator_AHouseguestWithoutANameCannotStart()
        {
            var before = director.Snapshot;
            yield return OpenCreator();

            var start = CastButtons(CharacterCreator.StartCaption);
            Assert.That(start, Has.Length.EqualTo(1));
            start[0].onClick.Invoke();
            yield return null;
            yield return null;

            Assert.That(Creator().IsShowing, Is.True, "It should still be open, with the reason on screen.");
            Assert.That(director.Snapshot.sessionId, Is.EqualTo(before.sessionId));
        }

        [UnityTest]
        public IEnumerator Creator_TheStatControlsSpendTheAllowanceAndStop()
        {
            yield return OpenCreator();
            CastButtons("Personality")[0].onClick.Invoke();
            yield return null;
            var creator = Creator();

            for (int i = 0; i < CharacterDraft.SparePoints; i++)
            {
                var raise = CastButtons(CharacterCreator.RaiseCaption("mental"));
                Assert.That(raise, Has.Length.EqualTo(1), "point " + i);
                raise[0].onClick.Invoke();
                yield return null;
            }

            Assert.That(creator.Draft.Remaining, Is.Zero);
            Assert.That(WebTraits.Get(creator.Draft.Stats, "mental"),
                Is.EqualTo(CharacterDraft.StartingStat + CharacterDraft.SparePoints));

            Assert.That(CastButtons(CharacterCreator.RaiseCaption("social")), Is.Empty,
                "Raising another stat is disabled once the allocation is spent.");
            yield return null;
            Assert.That(WebTraits.Get(creator.Draft.Stats, "social"), Is.EqualTo(CharacterDraft.StartingStat),
                "The allowance is spent, so the control does nothing rather than going into debt.");
        }

        [UnityTest]
        public IEnumerator Creator_ATraitRaisesTwoStatsAndGivesThemBack()
        {
            yield return OpenCreator();
            CastButtons("Personality")[0].onClick.Invoke();
            yield return null;
            var creator = Creator();

            CastButtons("Competitive")[0].onClick.Invoke();
            yield return null;
            Assert.That(creator.Draft.Traits, Does.Contain("Competitive"));
            Assert.That(WebTraits.Get(creator.Draft.Stats, "physical"), Is.EqualTo(7));
            Assert.That(WebTraits.Get(creator.Draft.Stats, "endurance"), Is.EqualTo(6));

            CastButtons("Competitive")[0].onClick.Invoke();
            yield return null;
            Assert.That(creator.Draft.Traits, Is.Empty);
            Assert.That(WebTraits.Get(creator.Draft.Stats, "physical"), Is.EqualTo(CharacterDraft.StartingStat));
        }

        /// <summary>The whole point of the screen: a season whose player is nobody on any card.</summary>
        [UnityTest]
        public IEnumerator Creator_StartingBuildsTheHouseguestThatWasBuilt()
        {
            var before = director.Snapshot;
            yield return OpenCreator();

            var creator = Creator();
            creator.Draft.Name = "Robin Vale";
            creator.Draft.AddTrait("Sneaky");

            CastButtons(CharacterCreator.StartCaption)[0].onClick.Invoke();
            yield return null;
            yield return null;

            Assert.That(Creator().IsShowing, Is.False);
            var state = director.Snapshot;
            Assert.That(state.sessionId, Is.Not.EqualTo(before.sessionId), "A new season should have started.");

            var you = state.Find(state.playerId);
            Assert.That(you.name, Is.EqualTo("Robin Vale"));
            Assert.That(you.traits, Does.Contain("Sneaky"));
            Assert.That(state.contestants.Count(c => c.isPlayer), Is.EqualTo(1));
            Assert.That(EpisodeValidation.TryValidate(state, out var error), Is.True, error);
        }

        [UnityTest]
        public IEnumerator Creator_CosmeticRemixPreservesBuildAndBackRetainsTheDraft()
        {
            yield return OpenCastScreen();
            var template = CastTemplates.Find("emma-brown");
            CastButtons(template.Name)[0].onClick.Invoke();
            yield return null;
            CastButtons(CharacterCreator.CustomiseCaption)[0].onClick.Invoke();
            yield return null;
            var original = CastTemplates.ToContestant(template, true);
            Assert.That(Creator().Draft.PreserveStats, Is.True);
            Assert.That(Creator().Draft.Appearance.presetId, Is.EqualTo(template.Id));
            CastButtons("Next starting look")[0].onClick.Invoke();
            yield return null;
            string appearance = Creator().Draft.Appearance.ContentKey();
            foreach (string stat in WebTraits.StatNames)
                Assert.That(WebTraits.Get(Creator().Draft.Stats, stat), Is.EqualTo(WebTraits.Get(original.stats, stat)), stat);
            Assert.That(Creator().Draft.Pronouns, Is.EqualTo(original.pronouns));
            Assert.That(Creator().Draft.Name, Is.EqualTo(original.name));

            CastButtons(CharacterCreator.BackCaption)[0].onClick.Invoke();
            yield return null;
            CastButtons("Resume setup")[0].onClick.Invoke();
            yield return null;
            Assert.That(Creator().Draft.Appearance.ContentKey(), Is.EqualTo(appearance));
            Assert.That(Creator().Draft.PreserveStats, Is.True);
        }
    }
}
