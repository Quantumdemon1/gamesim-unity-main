using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Gamesim.Episode;
using Gamesim.Simulation;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;

namespace Gamesim.Tests.PlayMode
{
    public sealed partial class EpisodePlayModeTests
    {
        /// <summary>
        /// The veto's draw as a screen (playtest, 2026-09-27): it was a quiet card with "HoH: X ·
        /// Nominees: A and B" in it. It takes the stage now, as the web's ChipDraw does: the Head of
        /// Household and both nominees as faces playing by right, a chip in the bag for each seat
        /// still to be drawn - and "Continue episode" still draws. The default house is six, where
        /// everyone plays and the engine draws nothing, so the bag is empty and nobody is in a pool
        /// (PACK8-PASS-PLAN B2): it showed three chips for a draw that never happened. A house with a
        /// draw is EpisodePlayModeTests.VetoDraw's.
        /// </summary>
        [UnityTest]
        public IEnumerator Ceremony_TheVetoDrawIsTheHouseAsFacesAndAChipBag()
        {
            string hoh = null;
            List<string> block = null;
            yield return InstallStrategySeason(45, state =>
            {
                var npcs = state.Active.Where(actor => !actor.isPlayer).Select(actor => actor.id).ToList();
                state.phase = EpisodePhase.VetoSelection;
                hoh = state.hohId = npcs[0];
                block = state.nominees = new List<string> { npcs[1], npcs[2] };
            });
            yield return OpenStation();
            yield return null;
            Canvas.ForceUpdateCanvases();
            var state = director.Snapshot;
            var panel = ActiveRect("Episode panel");
            AssertOnTheStage(panel);
            var title = ActiveRect(EpisodeHud.CeremonyTitleName);
            Assert.That(title != null && title.GetComponent<TMP_Text>().text == "Power of Veto Player Selection", Is.True, "The draw names itself.");

            string RoleOn(string id)
            {
                var card = panel.GetComponentsInChildren<RectTransform>().FirstOrDefault(rect => rect.name == "Face · " + state.Find(id).name);
                if (card == null) return null;
                var pill = card.GetComponentsInChildren<RectTransform>().FirstOrDefault(rect => rect.name == "Role");
                return pill != null ? pill.GetComponentInChildren<TMP_Text>().text : "";
            }
            Assert.That(RoleOn(hoh), Is.EqualTo("HOH"), "The Head of Household plays by right.");
            foreach (var nominee in block) Assert.That(RoleOn(nominee), Is.EqualTo("NOM"), "and so do both nominees.");

            int seats = EpisodeEngine.VetoPlayerCount(state.Active.Count());
            Assert.That(seats, Is.EqualTo(state.Active.Count()), "The default house is six: everyone plays.");
            int toDraw = state.Active.Count() > seats ? seats - 3 : 0;
            var bag = ActiveRect(EpisodeHud.ChipBagName);
            Assert.That(bag, Is.Not.Null, "The chips are in a bag.");
            Assert.That(bag.GetComponentsInChildren<RectTransform>().Count(rect => rect.name == "Chip"), Is.EqualTo(Mathf.Min(6, toDraw)),
                "a chip for each seat still to be drawn, and none when nobody is drawn.");
            foreach (var other in state.Active.Where(actor => actor.id != hoh && !block.Contains(actor.id)))
                Assert.That(RoleOn(other.id), Is.EqualTo(""), other.name + " plays too, with no role.");
            Assert.That(ActiveRect(EpisodeHud.VetoDrawLineName).GetComponent<TMP_Text>().text, Is.EqualTo(VetoDraw.EveryonePlaysLine),
                "The screen says everyone plays,");
            Assert.That(ActiveRect(EpisodeHud.DrawEligibleName), Is.Null, "and draws nobody from a pool.");

            var draw = ButtonWithCaption("Continue episode");
            Assert.That(draw.transform.parent, Is.SameAs(panel), "The way on is pinned.");
            draw.onClick.Invoke();
            yield return null;
            Assert.That(director.Snapshot.phase, Is.EqualTo(EpisodePhase.Veto), "Continue draws, and the veto is next.");
            Assert.That(director.Snapshot.vetoPlayers, Has.Count.EqualTo(seats));
            director.ClosePanels();
            yield return null;
        }

        /// <summary>
        /// The campaign as a screen (playtest, 2026-09-27): it was fourteen blocks of text down one
        /// column. It is the block as faces and the votes as cards now, each one press from walking
        /// over to talk; the whole house's meetings and listening in wait behind "More ways to
        /// campaign"; closing campaigning is still the pinned way on.
        /// </summary>
        [UnityTest]
        public IEnumerator Ceremony_TheCampaignIsTheVotesAsCardsWithTheRestBehindMore()
        {
            yield return InstallStrategySeason(46, state =>
            {
                AtVetoMeeting(state, false);
                state.phase = EpisodePhase.Campaign;
                state.vetoResolved = true;
            });
            yield return OpenStation();
            yield return null;
            Canvas.ForceUpdateCanvases();
            var state = director.Snapshot;
            var panel = ActiveRect("Episode panel");
            AssertOnTheStage(panel);
            var title = ActiveRect(EpisodeHud.CeremonyTitleName);
            Assert.That(title != null && title.GetComponent<TMP_Text>().text == "Campaign", Is.True, "The campaign names itself.");
            var voters = ActiveRect(EpisodeHud.CampaignVotersName);
            Assert.That(voters, Is.Not.Null, "The votes are a grid of cards.");
            var expected = EpisodeEngine.Voters(state).Where(actor => !actor.isPlayer).ToList();
            Assert.That(expected, Is.Not.Empty);
            foreach (var voter in expected)
            {
                var talk = ButtonWithCaption(EpisodeHud.CastTalkCaption(voter.name.Split(' ')[0]));
                Assert.That(talk.transform.IsChildOf(voters), Is.True, voter.name + " is a card with a way to talk to them.");
            }
            Assert.That(ButtonWithCaptionOrNull("Listen in on a conversation"), Is.Null, "The rest waits behind More.");
            Assert.That(ButtonWithCaption("Close campaigning and open voting").transform.parent, Is.SameAs(panel), "The way on is pinned.");
            // One screen now (PACK8-PASS-PLAN B4): the board fits the stage until More asks for the rest.
            AssertCampaignFits("The campaign");

            ButtonWithCaption(EpisodeDirector.CampaignMoreCaption).onClick.Invoke();
            yield return null;
            Assert.That(ButtonWithCaptionOrNull("Listen in on a conversation"), Is.Not.Null, "More opens the rest.");
            ButtonWithCaption(EpisodeDirector.CampaignLessCaption).onClick.Invoke();
            yield return null;
            Assert.That(ButtonWithCaptionOrNull("Listen in on a conversation"), Is.Null, "and Fewer puts it away.");

            string someone = expected[0].id;
            ButtonWithCaption(EpisodeHud.CastTalkCaption(expected[0].name.Split(' ')[0])).onClick.Invoke();
            yield return null;
            Assert.That(director.IsPhasePanelOpen, Is.False, "Talking closes the campaign,");
            Assert.That(director.WalkingToId == someone || director.TalkingToId == someone, Is.True, "and walks over to them.");
            director.ClosePanels();
            yield return null;
        }
    }
}
