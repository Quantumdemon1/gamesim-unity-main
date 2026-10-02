using System.Collections;
using System.Linq;
using Gamesim.Episode;
using Gamesim.House;
using Gamesim.Presentation;
using Gamesim.Simulation;
using NUnit.Framework;
using TMPro;
using UnityEngine.TestTools;

namespace Gamesim.Tests.PlayMode
{
    /// <summary>
    /// The finale in the house: the jury read one juror at a time while the house's chrome waits, a
    /// card when the house is down to three, and a card for the final Head of Household's choice with
    /// the new juror still in the room for it.
    /// </summary>
    public sealed partial class EpisodePlayModeTests
    {
        /// <summary>
        /// Stops the scene's own season committing while a fixture replaces it. The reload loads the
        /// scene asynchronously and the director it replaces keeps ticking its house until then: an
        /// autonomy commit in that window saved the scene's season over the fixture just written,
        /// and the test then played the wrong season. The reloaded director starts its house afresh.
        /// </summary>
        private void HoldTheHouseForTheFixture() => director.SuspendNpcAutonomyForDiagnostics();

        [UnityTest]
        public IEnumerator Finale_TheWinnersCommitReadsTheJuryWhileTheHouseWaits()
        {
            HoldTheHouseForTheFixture();
            yield return InstallFinaleFixture(true);
            yield return OpenFinalePanel();
            ButtonWithCaption(EpisodeHud.JurySkipCaption).onClick.Invoke();
            yield return Frames(2);
            ButtonWithCaption(EpisodeHud.SpeechSkipCaption).onClick.Invoke();
            yield return Frames(2);
            ButtonWithCaption(EpisodeHud.SpeechContinueCaption).onClick.Invoke();
            yield return Frames(2);
            Assert.That(director.Snapshot.phase, Is.EqualTo(EpisodePhase.Jury));

            ButtonWithCaption("Continue episode").onClick.Invoke();
            yield return Frames(2);
            var state = director.Snapshot;
            Assert.That(state.phase, Is.EqualTo(EpisodePhase.Finished));
            var reveal = SceneComponents<JuryReveal>().Single();
            Assert.That(reveal.IsPlaying, Is.True, "The winner is read out, not announced.");
            Assert.That(Hud.IsHeldForReveal, Is.True, "The house's chrome waits: it already knows who won.");
            Assert.That(SceneComponents<CeremonyTakeover>().Any(card => card.IsPlaying), Is.False, "The old winner's card does not play over it.");
            Assert.That(director.DepartingId, Is.Null, "Nobody is leaving: both finalists stop being active, and both stay.");
            var expected = EpisodeDirector.JuryOrder(state.votes.Select(vote => vote.voterId), state.seed)
                .Select(id => state.Find(id).name.Split(' ')[0]).ToArray();
            Assert.That(reveal.GetComponentsInChildren<TMP_Text>(true).Where(label => label.name == "Juror").Select(label => label.text),
                Is.EqualTo(expected), "The jury, read in the season's own deal.");

            yield return SkipReveals();
            Assert.That(ShownText(), Does.Contain("Winner: " + state.Find(state.winnerId).name), "Then the finale's panel says it.");
        }

        [UnityTest]
        public IEnumerator Finale_AJurorsOwnVoteIsReadWithTheRest()
        {
            HoldTheHouseForTheFixture();
            yield return InstallFinaleFixture(false, "Jordan Bell");
            yield return OpenFinalePanel();
            ButtonWithCaption(EpisodeHud.JurySkipCaption).onClick.Invoke();
            yield return Frames(2);
            ButtonWithCaption(EpisodeHud.SpeechContinueCaption).onClick.Invoke();
            yield return Frames(2);
            var before = director.Snapshot;
            Assert.That(before.phase, Is.EqualTo(EpisodePhase.Jury));
            Assert.That(before.votes, Is.Empty, "No juror has voted before the player.");
            var finalist = before.Active.First();
            ButtonWithCaption("Vote for " + finalist.name + " to win").onClick.Invoke();
            yield return Frames(2);
            ButtonWithCaption("Continue episode").onClick.Invoke();
            yield return Frames(2);
            var reveal = SceneComponents<JuryReveal>().Single();
            Assert.That(reveal.IsPlaying, Is.True);
            var jurors = reveal.GetComponentsInChildren<TMP_Text>(true).Where(label => label.name == "Juror").Select(label => label.text).ToList();
            Assert.That(jurors, Does.Contain("You"), "The player's own vote is read with the jury's, as theirs,");
            Assert.That(jurors, Does.Not.Contain("Jordan"), "not under their name.");
            Assert.That(director.DepartingId, Is.Null, "Neither finalist is taken for somebody leaving.");
            yield return SkipReveals();
        }

        [UnityTest]
        public IEnumerator Finale_TheHouseDownToThreeOpensWithTheFinalThreeCard()
        {
            HoldTheHouseForTheFixture();
            yield return InstallDiaryFixture(state => state.phase == EpisodePhase.Social && state.Active.Count() == 3
                && state.pendingDiary == null && !HouseEvents.Ready(state), "the social week before the final three");
            yield return OpenFinalePanel();
            ButtonWithCaption("Begin the next competition").onClick.Invoke();
            yield return Frames(2);
            Assert.That(director.Snapshot.phase, Is.EqualTo(EpisodePhase.FinalHoHPart1));
            var takeover = SceneComponents<CeremonyTakeover>().Single();
            Assert.That(takeover.IsPlaying, Is.True, "The finale opens with a card.");
            Assert.That(Hud.IsHeldForReveal, Is.True, "The card has the frame to itself: no chrome under it (MOCKUP-PASS-PLAN M2).");
            var texts = takeover.GetComponentsInChildren<TMP_Text>(true).Select(label => label.text).ToList();
            Assert.That(texts, Does.Contain(CeremonyTakeover.TitleFor(CeremonyTakeover.FinalThreeKind)));
            Assert.That(texts, Does.Contain("Only three remain. The final battle for power begins now."));
            Assert.That(texts.Count(text => text == "FINAL 3"), Is.EqualTo(3), "The three of them, each badged.");
            takeover.Cancel();
            yield return Frames(2);
            Assert.That(Hud.IsHeldForReveal, Is.False, "The chrome is back once the card is down.");
        }

        [UnityTest]
        public IEnumerator Finale_TheFinalHoHsChoiceIsACardAndTheNewJurorStaysForIt()
        {
            HoldTheHouseForTheFixture();
            yield return InstallDiaryFixture(state => state.phase == EpisodePhase.FinalEviction && state.hohId == state.playerId,
                "the player as final Head of Household");
            yield return OpenFinalePanel();
            // The world a played season carries by now, which the walk out borrows its people from:
            // a fixture installed at the final eviction has none of its own.
            director.BuildNpcWorldForDiagnostics();
            director.WalkOutsInBatchRuns = true;
            Assert.That(director.NpcAutonomyDiagnostic, Is.Null);
            var state = director.Snapshot;
            var cut = state.Active.First(actor => !actor.isPlayer);
            var kept = state.Active.Single(actor => !actor.isPlayer && actor.id != cut.id);
            var cutBody = SceneComponents<HouseNpc>().Single(npc => npc.Id == cut.id);
            var stoodAt = cutBody.transform.position;
            ButtonWithCaption(FinalChoiceWords.CaptionToEvict(state, cut.id)).onClick.Invoke();
            yield return Frames(2);
            Assert.That(director.Snapshot.Find(cut.id).status, Is.EqualTo(ContestantStatus.Jury),
                "One press commits the choice: the ring and the gold edge on its column are decoration (MOCKUP-PASS M8).");
            Assert.That(Flat(cutBody.transform.position, stoodAt), Is.LessThan(0.3f),
                "The new juror is not taken to the jury while the card is about them.");
            var takeover = SceneComponents<CeremonyTakeover>().Single();
            Assert.That(takeover.IsPlaying, Is.True, "The choice gets a card: it used to pass without one.");
            Assert.That(Hud.IsHeldForReveal, Is.True, "and the frame to itself: no chrome under it (MOCKUP-PASS-PLAN M2).");
            var texts = takeover.GetComponentsInChildren<TMP_Text>(true).Select(label => label.text).ToList();
            Assert.That(texts, Does.Contain(CeremonyTakeover.TitleFor(CeremonySting.FinalEvictionKind)));
            Assert.That(texts, Does.Contain("FINAL HOH").And.Contain("FINAL 2").And.Contain("JURY"));
            Assert.That(director.DepartingId, Is.EqualTo(cut.id), "The new juror stays in the room for it.");
            Assert.That(SceneComponents<HouseNpc>().Any(npc => npc.Id == cut.id && npc.gameObject.activeInHierarchy), Is.True);
            Assert.That(SceneComponents<HouseNpc>().Any(npc => npc.Id == kept.id && npc.gameObject.activeInHierarchy), Is.True);
            takeover.Cancel();
            foreach (var sting in SceneComponents<CeremonySting>()) sting.Cancel();
            yield return Frames(3);
            Assert.That(director.DepartingId, Is.Null, "and goes when the card does:");
            Assert.That(Hud.IsHeldForReveal, Is.False, "the chrome back with them,");
            Assert.That(director.WalkingOutId, Is.Null, "not out of the door, since this eviction opens finale night,");
            Assert.That(director.NpcAutonomyDiagnostic, Is.Null);
            Assert.That(cutBody.gameObject.activeInHierarchy, Is.True, "but to the jury in the living room.");
            Assert.That(HouseRoomQuery.TryCreate(director.gameObject.scene, out var rooms, out var why), Is.True, why);
            Assert.That(rooms.TryLocate(cutBody.transform.position, 0.35f, out var room) ? room : "nowhere", Is.EqualTo("Living"));
        }
    }
}
