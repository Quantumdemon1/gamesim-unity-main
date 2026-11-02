using Gamesim.Episode;
using Gamesim.House;
using NUnit.Framework;
using UnityEngine;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// The walk-over holds (OFFERS-COME-TO-YOU-PLAN slice 3), in its three pure rules: a visit that answers from arrival
    /// walks on a budget of its own route (<see cref="HouseMeetingCoordinator.NpcInvitationTravelBudget"/>); letting go
    /// of an ask hands back only a heading the ask gave (<see cref="ComesToYouFacing"/>); and the line's text boxes are at
    /// least 1.3x their font (<see cref="HouseNpcInvitationBubble.LabelBoxHeight"/>). The house's own walking, arriving
    /// and drawing are PlayMode's (HouseNpcInvitationPlayModeTests, EpisodePlayModeTests.ComesToYou). EditMode only: the
    /// rules are in Runtime.
    /// </summary>
    public sealed class ComesToYouWalkOverTests
    {
        [Test]
        public void TheTravelBudgetFollowsTheRoute()
        {
            // clamp(route / 2.2 m/s x 1.5 + 3 s, 6 s, 30 s). The design's numbers are literals here: a test that only
            // compared the budget with the constants would follow a changed constant wherever it went.
            Assert.That(HouseMeetingCoordinator.NpcInvitationTravelFactor, Is.EqualTo(1.5f), "Half again the walk.");
            Assert.That(HouseMeetingCoordinator.NpcInvitationTravelSlack, Is.EqualTo(3f), "3 s to spare.");
            Assert.That(HouseMeetingCoordinator.NpcInvitationTravelMin, Is.EqualTo(6f), "Never under 6 s.");
            Assert.That(HouseMeetingCoordinator.NpcInvitationTravelMax, Is.EqualTo(30f), "Never over 30 s.");
            Assert.That(HouseMeetingCoordinator.NpcInvitationLifetime, Is.EqualTo(25f), "The answer window is the visit's old 25 s.");
            Assert.That(HouseMeetingCoordinator.NpcInvitationTravelBudget(0f), Is.EqualTo(6f), "No route at all: the least budget, 6 s.");
            Assert.That(HouseMeetingCoordinator.NpcInvitationTravelBudget(2.2f), Is.EqualTo(6f), "2.2 m would be 4.5 s: 6 s.");
            Assert.That(HouseMeetingCoordinator.NpcInvitationTravelBudget(100f), Is.EqualTo(30f), "100 m would be 71 s: 30 s.");
            Assert.That(HouseMeetingCoordinator.NpcInvitationTravelBudget(60f), Is.EqualTo(30f), "60 m would be 44 s: 30 s.");
            Assert.That(HouseMeetingCoordinator.NpcInvitationTravelBudget(float.NaN), Is.EqualTo(30f), "A route with no length: the most, 30 s.");
            Assert.That(HouseMeetingCoordinator.NpcInvitationTravelBudget(8.8f), Is.EqualTo(9f).Within(1e-4f),
                "8.8 m is 4 s at walking pace: 6 s with half again, and 3 s to spare.");
            Assert.That(HouseMeetingCoordinator.NpcInvitationTravelBudget(22f), Is.EqualTo(18f).Within(1e-4f), "22 m: 15 s and 3 s to spare.");
            Assert.That(HouseMeetingCoordinator.NpcInvitationTravelBudget(2.2f), Is.EqualTo(HouseMeetingCoordinator.NpcInvitationTravelMin),
                "A step or two still gets the least budget.");
            Assert.That(HouseMeetingCoordinator.NpcInvitationTravelBudget(0f), Is.EqualTo(HouseMeetingCoordinator.NpcInvitationTravelMin));
            Assert.That(HouseMeetingCoordinator.NpcInvitationTravelBudget(100f), Is.EqualTo(HouseMeetingCoordinator.NpcInvitationTravelMax),
                "However far, never more than the most.");
            Assert.That(HouseMeetingCoordinator.NpcInvitationTravelBudget(float.NaN), Is.EqualTo(HouseMeetingCoordinator.NpcInvitationTravelMax));
            Assert.That(HouseMeetingCoordinator.NpcInvitationTravelBudget(float.PositiveInfinity), Is.EqualTo(HouseMeetingCoordinator.NpcInvitationTravelMax));
            float before = 0f;
            for (float route = 0f; route <= 60f; route += .5f)
            {
                float budget = HouseMeetingCoordinator.NpcInvitationTravelBudget(route);
                Assert.That(budget, Is.GreaterThanOrEqualTo(before), "A longer route never has less time: " + route + " m.");
                Assert.That(budget, Is.GreaterThanOrEqualTo(route / HouseNpcMotion.DefaultSpeed)
                    .Or.EqualTo(HouseMeetingCoordinator.NpcInvitationTravelMax), "The walk itself fits its budget: " + route + " m.");
                before = budget;
            }
        }

        [Test]
        public void LettingGoHandsBackOnlyTheAsksOwnHeading()
        {
            Assert.That(ComesToYouFacing.StillGiven(90f, 90f), Is.True, "Still on the heading the ask gave: it goes with the ask.");
            Assert.That(ComesToYouFacing.StillGiven(90.000001f, 90f), Is.True, "The same heading, as a float reads it.");
            Assert.That(ComesToYouFacing.StillGiven(45f, 90f), Is.False, "Turned since - a talk spot, a ceremony, a seat: theirs stays.");
            Assert.That(ComesToYouFacing.StillGiven(float.NaN, 90f), Is.False, "Already let go by somebody else: nothing to hand back.");
            Assert.That(ComesToYouFacing.StillGiven(90f, float.NaN), Is.False, "The ask gave no heading: whatever the body holds is not the ask's.");
            Assert.That(ComesToYouFacing.StillGiven(float.NaN, float.NaN), Is.False);
        }

        [Test]
        public void ALineBoxIsAtLeastAThirdTallerThanItsFont()
        {
            foreach (float font in new[] { 12f, 16f, 17f, 19f, 16f * 1.4f, 17f * 1.4f, 19f * 1.4f })
            {
                float box = HouseNpcInvitationBubble.LabelBoxHeight(font, font * 1.21f);
                Assert.That(box, Is.GreaterThanOrEqualTo(font * 1.3f), "A one-line box at " + font + ": " + box);
                Assert.That(box, Is.EqualTo(Mathf.Ceil(font * 1.32f) + 1f), "ceil(1.32 x the font) + 1 at " + font);
            }
            Assert.That(HouseNpcInvitationBubble.LabelBoxHeight(19f, 80f), Is.EqualTo(80f), "Text that needs more gets more.");
        }
    }
}
