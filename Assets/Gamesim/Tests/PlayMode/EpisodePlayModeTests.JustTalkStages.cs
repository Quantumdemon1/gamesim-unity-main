using System.Collections;
using System.Linq;
using Gamesim.Episode;
using Gamesim.Simulation;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;

namespace Gamesim.Tests.PlayMode
{
    /// <summary>
    /// The free "Just talk" screen at talk rules 2 (free talk slice 2), the two things only a built screen shows: a housemate's
    /// notebook page carries the "Between you" line (their friendship stage and the chemistry) and the player's own card the
    /// "How you come across" line; at talk rules 1, with the same talk behind them, neither is drawn. The fixture installs the
    /// level itself (fresh seasons are enrolled only at integration) and holds the house still.
    /// </summary>
    public sealed partial class EpisodePlayModeTests
    {
        /// <summary>A house at a talk level with three counted talks, rapport 6 and agreement 4 behind the first housemate; their id.</summary>
        private IEnumerator InstallBondedTalkHouse(int talkRules, System.Action<string> bonded)
        {
            string id = null;
            yield return InstallTalkingHouse(8, false, s =>
            {
                EpisodeEngine.EnableWeek(s); EpisodeEngine.EnableStory(s);
                s.story.talkRules = talkRules;
                id = s.contestants.First(c => !c.isPlayer && c.status == ContestantStatus.Active).id;
                var contact = s.story.contacts.FirstOrDefault(c => c.npcId == id);
                if (contact == null) s.story.contacts.Add(contact = new ContactState { npcId = id });
                contact.rapport = 6;
                contact.talks = 3;
                contact.agreement = 4;
            });
            Assert.That(director.Snapshot.story.talkRules, Is.EqualTo(talkRules), "The fixture came back at its talk rules.");
            bonded(id);
        }

        private string[] BlockLabels(string heading)
        {
            var card = LastActive(EpisodeHud.ProfileBlockPrefix + heading);
            return card == null ? null : card.GetComponentsInChildren<TMP_Text>().Select(t => t.text).ToArray();
        }

        [UnityTest]
        public IEnumerator JustTalkStages_AtTalkRulesTwoThePagesSayWhereYouStandAndHowTheHouseReadsYou()
        {
            string id = null;
            yield return InstallBondedTalkHouse(TalkRules.Chemistry, x => id = x);
            var state = director.Snapshot;
            int revision = state.revision;
            string bond = TalkScreenLines.BondLine(TalkNotes.BondWith(state, id));
            Assert.That(bond, Is.Not.Null.And.Contain(TalkNotes.ChemistryLabel + ": "), "The housemate holds a stage and shows the chemistry.");

            director.ShowHouseguestProfile(id);
            yield return null;
            Canvas.ForceUpdateCanvases();
            var labels = BlockLabels(TalkScreenLines.BetweenYouHeading);
            Assert.That(labels, Is.Not.Null, "The housemate's page has the 'Between you' block.");
            Assert.That(labels, Has.Some.EqualTo(bond), "It says the stage and the chemistry in one line.");

            director.ShowYourCard();
            yield return null;
            Canvas.ForceUpdateCanvases();
            string read = TalkNotes.HouseReadsYou(director.Snapshot);
            Assert.That(read, Does.StartWith(TalkLean.ReadsYouAs));
            labels = BlockLabels(TalkScreenLines.YourReadHeading);
            Assert.That(labels, Is.Not.Null, "The player's own card has the 'How you come across' block.");
            Assert.That(labels, Has.Some.EqualTo(read));
            Assert.That(director.Snapshot.revision, Is.EqualTo(revision), "Reading the pages commits nothing.");
        }

        [UnityTest]
        public IEnumerator JustTalkStages_AtTalkRulesOneNeitherLineIsDrawn()
        {
            string id = null;
            yield return InstallBondedTalkHouse(TalkRules.Talk, x => id = x);
            director.ShowHouseguestProfile(id);
            yield return null;
            Canvas.ForceUpdateCanvases();
            Assert.That(LastActive(EpisodeHud.ProfileIdentityName), Is.Not.Null, "The profile is open.");
            Assert.That(LastActive(EpisodeHud.ProfileBlockPrefix + TalkScreenLines.BetweenYouHeading), Is.Null, "No stage line at talk rules 1.");

            director.ShowYourCard();
            yield return null;
            Canvas.ForceUpdateCanvases();
            Assert.That(LastActive(EpisodeHud.ProfileIdentityName), Is.Not.Null, "The player's card is open.");
            Assert.That(LastActive(EpisodeHud.ProfileBlockPrefix + TalkScreenLines.YourReadHeading), Is.Null, "No read of the player at talk rules 1.");
        }
    }
}
