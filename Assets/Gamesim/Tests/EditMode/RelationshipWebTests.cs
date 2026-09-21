using System.Collections.Generic;
using System.Linq;
using Gamesim.Presentation;
using Gamesim.Simulation;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// The reading behind the relationship web: which edge a relationship gets, who counts as an
    /// ally or a rival, and whose relationships the column reads.
    ///
    /// <para>Every one of these is bounded by the player's own record. The test that matters most
    /// is that an NPC's feeling about the player, which the engine stores and the player cannot
    /// know, never leaks into the picture.</para>
    /// </summary>
    public sealed class RelationshipWebTests
    {
        private const string You = "you";

        [SetUp]
        public void Fresh() => RelationshipWeb.ClearSelection();

        [TearDown]
        public void Restore() => RelationshipWeb.ClearSelection();

        [Test]
        public void FiltersUseOnlyPlayerKnowledgeAndResetForAnotherSeason()
        {
            var state=House();
            RelationshipWeb.SetFilter(state,RelationshipWeb.Filter.Allies);
            Assert.That(RelationshipWeb.FilteredOthers(state).Select(actor=>actor.id),Is.EqualTo(new[]{"b"}));
            RelationshipWeb.SetFilter(state,RelationshipWeb.Filter.Friends);
            Assert.That(RelationshipWeb.FilteredOthers(state).Select(actor=>actor.id),Is.EqualTo(new[]{"a"}),
                "A housemate's secret dislike cannot leak through a tension filter.");
            RelationshipWeb.Select(state,"a");
            RelationshipWeb.SetFilter(state,RelationshipWeb.Filter.Tension);
            Assert.That(RelationshipWeb.FilteredOthers(state).Select(actor=>actor.id),Is.EqualTo(new[]{"c","d"}));
            Assert.That(RelationshipWeb.SelectedFor(state).id,Is.EqualTo(You),"A hidden selection returns to the player.");
            state.sessionId="a-new-season";
            Assert.That(RelationshipWeb.FilteredOthers(state).Count,Is.EqualTo(RelationshipWeb.Others(state).Count));
        }

        /// <summary>A house of five with the player's reading of each set by hand.</summary>
        private static EpisodeState House()
        {
            var state = new EpisodeState { sessionId = "season-a", playerId = You };
            void Add(string id, string name, ContestantStatus status = ContestantStatus.Active) =>
                state.contestants.Add(new ContestantState { id = id, name = name, status = status, isPlayer = id == You });
            Add(You, "Alex Rivera");
            Add("a", "Maya Hassan");
            Add("b", "Jordan Lee");
            Add("c", "Casey Park");
            Add("d", "Tyler Brooks");
            Add("e", "Emma Stone", ContestantStatus.Evicted);
            Add("f", "Riley Johnson");

            void Feel(string from, string to, double score) =>
                state.relationships.Add(new RelationshipState { fromId = from, toId = to, score = score });
            Feel(You, "a", 40);
            Feel("a", You, -60); // Maya cannot stand the player. The player does not know that.
            Feel(You, "b", 20);
            Feel(You, "c", -20);
            Feel(You, "d", -50);
            Feel(You, "e", 80);  // Evicted: the warmest reading in the save, and out of the house.
            // "f" has no relationship record at all.

            state.alliances.Add(new AllianceState { id = "x", name = "The Core", members = new List<string> { You, "b" }, active = true });
            return state;
        }

        [Test]
        public void EdgeKinds_ReadThePlayersOwnOutboundScoreAndNothingElse()
        {
            var state = House();
            Assert.That(RelationshipWeb.KindOf(state, "a"), Is.EqualTo(RelationshipWeb.Kind.Friendship),
                "Maya's -60 towards the player is her business; the player's 40 is a friendship.");
            Assert.That(RelationshipWeb.KindOf(state, "b"), Is.EqualTo(RelationshipWeb.Kind.Alliance),
                "A shared active alliance outranks the score.");
            Assert.That(RelationshipWeb.KindOf(state, "c"), Is.EqualTo(RelationshipWeb.Kind.Distrust));
            Assert.That(RelationshipWeb.KindOf(state, "d"), Is.EqualTo(RelationshipWeb.Kind.Rivalry));
            Assert.That(RelationshipWeb.KindOf(state, "f"), Is.EqualTo(RelationshipWeb.Kind.Neutral),
                "No record is neutral, not absent.");
            Assert.That(RelationshipWeb.KindOf(state, You), Is.EqualTo(RelationshipWeb.Kind.Neutral));
        }

        [Test]
        public void AlliesAndRivals_AreOrderedByStrengthAndLeaveOutTheEvicted()
        {
            var state = House();
            Assert.That(RelationshipWeb.Allies(state).Select(c => c.id), Is.EqualTo(new[] { "a", "b" }),
                "Closest first; Emma's 80 does not count because she is out of the house.");
            Assert.That(RelationshipWeb.Rivals(state).Select(c => c.id), Is.EqualTo(new[] { "d", "c" }),
                "Most hostile first.");
            Assert.That(RelationshipWeb.Others(state).Select(c => c.id), Is.EqualTo(new[] { "a", "b", "c", "d", "f" }),
                "The web draws everyone still playing, in cast order, and never the player twice.");
        }

        [Test]
        public void TheWords_FollowTheThresholds()
        {
            var state = House();
            Assert.That(RelationshipWeb.AllyWord(state, "b"), Is.EqualTo("Strong alliance"));
            Assert.That(RelationshipWeb.AllyWord(state, "a"), Is.EqualTo("Close friend"));
            state.relationships.Single(r => r.fromId == You && r.toId == "a").score = 20;
            Assert.That(RelationshipWeb.AllyWord(state, "a"), Is.EqualTo("Friendly"));
            Assert.That(RelationshipWeb.RivalWord(state, "d"), Is.EqualTo("High tension"));
            Assert.That(RelationshipWeb.RivalWord(state, "c"), Is.EqualTo("Distrust"));
            Assert.That(RelationshipWeb.StandingWord(RelationshipWeb.Kind.Rivalry), Is.EqualTo("Hostile"));
            Assert.That(RelationshipWeb.StandingWord(RelationshipWeb.Kind.Alliance), Is.EqualTo("Allied"));
        }

        [Test]
        public void Selection_DefaultsToThePlayerAndFallsBackWhenTheChoiceNoLongerApplies()
        {
            var state = House();
            Assert.That(RelationshipWeb.SelectedFor(state).id, Is.EqualTo(You), "Nothing chosen reads the player.");

            RelationshipWeb.Select(state, "c");
            Assert.That(RelationshipWeb.Selected, Is.EqualTo("c"));
            Assert.That(RelationshipWeb.SelectedFor(state).id, Is.EqualTo("c"));

            state.Find("c").status = ContestantStatus.Evicted;
            Assert.That(RelationshipWeb.SelectedFor(state).id, Is.EqualTo(You),
                "A houseguest who has left the house cannot stay selected.");

            RelationshipWeb.Select(state, "a");
            var another = House();
            another.sessionId = "season-b";
            Assert.That(RelationshipWeb.SelectedFor(another).id, Is.EqualTo(You),
                "A selection belongs to the season it was made in.");

            RelationshipWeb.ClearSelection();
            Assert.That(RelationshipWeb.Selected, Is.Null);
        }

        [Test]
        public void NodeName_DecoratesAHouseguestsNameWithoutChangingIt()
        {
            Assert.That(RelationshipWeb.NodeName("Maya Hassan"), Does.StartWith("Maya Hassan"));
            Assert.That(RelationshipWeb.NodeName("Maya Hassan"), Is.Not.EqualTo("Maya Hassan"),
                "The cast rail already owns the bare name as a control; the node must not collide with it.");
        }

        [Test]
        public void MoodGlyphs_MapEveryMoodTheEngineUsesToADrawnFace()
        {
            foreach (var mood in new[] { "Angry", "Upset", "Neutral", "Content", "Happy" })
                Assert.That(RelationshipWeb.MoodIcon(mood), Does.StartWith("mood-"), mood);
            Assert.That(RelationshipWeb.MoodIcon("Upset"), Is.EqualTo("mood-sad"));
            Assert.That(RelationshipWeb.MoodIcon(null), Is.EqualTo("mood-neutral"));
            Assert.That(RelationshipWeb.MoodColour("Angry"), Is.EqualTo(UiTheme.Conflict));
            Assert.That(RelationshipWeb.MoodColour("Happy"), Is.EqualTo(UiTheme.Allied));
        }
    }
}
