using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Gamesim.Episode;
using Gamesim.Persistence;
using Gamesim.Presentation;
using Gamesim.Simulation;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;

namespace Gamesim.Tests.PlayMode
{
    /// <summary>
    /// Walk-ups name the houseguest (ComesToYou.Source.Story): a story beat whose words have a houseguest come to the player
    /// is brought by that houseguest, who walks up and says so, in place of the Pull's card that names nobody; "What's on
    /// your mind?" opens the beat's scene card where the player stands, the card "Hear them out" opens, whose answers send
    /// the command they always sent. The test opts in after the fixture's reload turns asks off (AsksEnabled set
    /// explicitly), pins scaled time with PinnedClock, holds the house's pairing as the other walk-up tests do and never
    /// ticks the NPC world by hand. The house is past free time, so its world is built for the test
    /// (BuildNpcWorldForDiagnostics). The season turns the story on itself (the fixture's plays none).
    /// </summary>
    public sealed partial class EpisodePlayModeTests
    {
        /// <summary>The lobby beat <see cref="SaveComesToYouStoryState"/> opened, and its lobbyist.</summary>
        private string comesStoryBeatId, comesStorySpeakerId;

        /// <summary>
        /// A played season in the nomination before any name, a houseguest Head of Household, from week 2 (a first week opens
        /// only its own arc): the story switched on there, so nothing else is open, and the lobby opened by the engine's own
        /// seam - a houseguest who "catches you in the hallway" before the names. Saved.
        /// </summary>
        private void SaveComesToYouStoryState()
        {
            EpisodeState fixture = null;
            for (uint seed = 1; seed <= 60 && fixture == null; seed++)
            {
                var engine = new EpisodeEngine(ContentCatalog.Create(seed));
                for (int guard = 0; guard < 80; guard++)
                {
                    var current = engine.Snapshot;
                    if (current.week >= 2 && current.phase == EpisodePhase.Nomination && current.nominees.Count == 0
                        && current.hohId != current.playerId && current.Find(current.playerId).status == ContestantStatus.Active)
                    {
                        EpisodeEngine.EnableStory(current, current.week);
                        if (EpisodeEngine.StartStory(current, "the-lobby", StoryAnchors.HohCrowned)
                            && EpisodeEngine.OpenStoryBeats(current).Count == 1) fixture = current;
                        break;
                    }
                    if (current.phase == EpisodePhase.Finished) break;
                    Assert.That(engine.Apply(NextCommand(current)).accepted, Is.True);
                }
            }
            Assert.That(fixture, Is.Not.Null, "No bounded legal fixture with a lobbyist's word found.");
            var beat = EpisodeEngine.OpenStoryBeats(fixture).Single();
            comesStoryBeatId = beat.id;
            comesStorySpeakerId = beat.cast.Single(part => part.role == "LOBBYIST").contestantId;
            Assert.That(ComesToYou.StorySpeaker(fixture, beat), Is.EqualTo(comesStorySpeakerId), "The premise: the lobbyist can bring it.");
            Assert.That(ComesToYou.Read(fixture).Where(ask => ask.source == ComesToYou.Source.Story).Select(ask => ask.recordId),
                Is.EqualTo(new[] { beat.id }), "The reader lists the beat as the lobbyist's.");
            Assert.That(EpisodeValidation.TryValidate(fixture, out string reason), Is.True, reason);
            new EpisodeSaveStore(director.SavePath).Save(fixture);
        }

        /// <summary>
        /// The story house saved and reloaded, its world built (the house is past free time), asks on and taken effect with
        /// the house's pairing held (TurnAsksOnInTheLoadedHouse). No visit waited for.
        /// </summary>
        private IEnumerator InstallComesToYouStoryHouse()
        {
            SaveComesToYouStoryState();
            yield return ReloadEpisode();
            director.BuildNpcWorldForDiagnostics();
            Assert.That(director.NpcAutonomyDiagnostic, Is.Null, "The house's world is built.");
            yield return TurnAsksOnInTheLoadedHouse(new string[0], true);
            Assert.That(EpisodeEngine.OpenStoryBeats(director.Snapshot).Select(item => item.id), Is.EqualTo(new[] { comesStoryBeatId }),
                "The beat survived the load.");
        }

        // ------------------------------------------------------------------ ST-P1. the speaker walks up, and hearing them opens the scene card

        [UnityTest]
        public IEnumerator ComesToYou_Story_WalksUpAndOpensTheSceneCard() => PinnedClock(AStoryBeatWalksUpHeld);

        private IEnumerator AStoryBeatWalksUpHeld() => WithTheHouseHeld(InstallComesToYouStoryHouse, AStoryBeatWalksUp);

        private IEnumerator AStoryBeatWalksUp()
        {
            string beatId = comesStoryBeatId, speakerId = comesStorySpeakerId;
            // The played season's offers wait too: held aside, so the lobbyist's beat is the one ask that can come.
            var aside = NpcRead<HashSet<string>>("askSetAside");
            foreach (var ask in ComesToYou.Read(director.Snapshot).Where(row => row.source != ComesToYou.Source.Story)) aside.Add(ask.recordId);

            yield return WaitForTheFirstVisit(new string[0]);
            Assert.That(director.CurrentNpcInvitationRecordId, Is.EqualTo(beatId), "The lobbyist sets out with the beat.");
            Assert.That(director.CurrentAskerId, Is.EqualTo(speakerId));
            Assert.That(director.IsAskCardOpen, Is.False, "In person, not as a card.");
            Assert.That(director.PullOffered, Is.Null, "With asks on the beat is no Pull: the houseguest brings it.");

            // The line held up (the auto-open waits) until the bubble shows.
            float deadline = Time.realtimeSinceStartup + 35f;
            while (!director.NpcInvitationIsVisible && Time.realtimeSinceStartup < deadline)
            {
                NpcWrite("askSpokeFor", 0f);
                yield return null;
            }
            NpcWrite("askSpokeFor", 0f);
            Assert.That(director.NpcInvitationIsVisible, Is.True, "The lobbyist's line is up.");
            var bubble = NpcRead<HouseNpcInvitationBubble>("npcInvitationBubble");
            Assert.That(bubble.CurrentText, Is.EqualTo(ComesToYou.StoryLine), "The lobbyist says the story's line.");
            var name = bubble.GetComponentsInChildren<TMP_Text>(true).Last(label => label.name == "Invitation speaker" && label.gameObject.activeInHierarchy);
            Assert.That(name.text, Is.EqualTo(Localisation.Text(director.Snapshot.Find(speakerId).name)), "The bubble names who came.");
            Assert.That(bubble.HearControl.name, Is.EqualTo(HouseNpcInvitationBubble.StoryHearCaption), "The bubble's yes is the story's own caption,");
            Assert.That(bubble.NotNowControl.name, Is.EqualTo(HouseNpcInvitationBubble.StoryNotNowCaption), "and its no.");
            Assert.That(FindButton(HouseNpcInvitationBubble.StoryHearCaption), Is.SameAs(bubble.HearControl), "One control wears the yes.");
            Assert.That(FindButton(HouseNpcInvitationBubble.StoryNotNowCaption), Is.SameAs(bubble.NotNowControl), "One control wears the no.");
            var pullCaptions = new[] { EpisodeHud.StepInCaption, EpisodeHud.StayOutCaption, EpisodeHud.HearThemOutCaption,
                EpisodeHud.NotNowCaption, EpisodeHud.JoinMeetingCaption, EpisodeHud.TakeItOnCaption };
            foreach (string own in new[] { HouseNpcInvitationBubble.StoryHearCaption, HouseNpcInvitationBubble.StoryNotNowCaption })
                Assert.That(pullCaptions, Has.No.Member(own), "The bubble's '" + own + "' is none of the Pull's captions.");
            Assert.That(ButtonWithCaptionOrNull(EpisodeHud.HearThemOutCaption), Is.Null, "No Pull card says \"Hear them out\" meanwhile.");

            // What's on your mind?: the scene card where the player stands, nothing committed or written.
            var before = director.Snapshot;
            byte[] bytes = File.ReadAllBytes(director.SavePath);
            bubble.HearControl.onClick.Invoke();
            Assert.That(director.IsSceneCardOpen, Is.True, "Hearing them out opens the beat's scene card where the player stands.");
            Assert.That(director.TalkingToId, Is.Null, "No conversation opens,");
            Assert.That(director.IsAskCardOpen, Is.False, "and no ask card.");
            Assert.That(director.IsAskSetAside(beatId), Is.True, "The beat is set aside for the phase.");
            AssertEquivalent(before, director.Snapshot);
            CollectionAssert.AreEqual(bytes, File.ReadAllBytes(director.SavePath), "Hearing commits nothing.");
            yield return null;
            Assert.That(ActiveRect(EpisodeHud.StoryChoicesName), Is.Not.Null, "The card carries the beat's choices.");

            // An answer there is the beat's own: the command it always sent.
            var open = EpisodeEngine.OpenStoryBeats(director.Snapshot).Single(item => item.id == beatId);
            var option = open.choices.First(c => !c.locked && !c.pickPerson && !c.conduct && !c.costsAction && c.optionId != open.lapseOptionId);
            var justBefore = director.Snapshot;
            ButtonWithCaption(EpisodeHud.EventChoiceCaption(option.label)).onClick.Invoke();
            var answered = director.Snapshot;
            Assert.That(answered.acceptedCommandIds.Count, Is.EqualTo(justBefore.acceptedCommandIds.Count + 1), "One command.");
            var expected = new EpisodeEngine(justBefore).Apply(new EpisodeCommand
            {
                id = answered.acceptedCommandIds.Last(), actorId = justBefore.playerId,
                expectedPhase = justBefore.phase, expectedRevision = justBefore.revision,
                kind = EpisodeCommandKind.ProgressStoryline, targetId = beatId, secondTargetId = option.optionId,
            });
            Assert.That(expected.accepted, Is.True, expected.reason);
            AssertEquivalent(expected.state, answered);
            Assert.That(EpisodeEngine.OpenStoryBeats(answered).Any(item => item.id == beatId), Is.False, "The beat is answered.");
            director.ClosePanels();
        }
    }
}
