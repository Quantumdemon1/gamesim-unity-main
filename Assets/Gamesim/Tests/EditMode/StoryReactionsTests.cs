using System;
using System.Linq;
using Gamesim.Episode;
using Gamesim.Presentation;
using Gamesim.Simulation;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// The story-reaction table (the art brief's R1): every row names a real beat and a real part
    /// of it, every story beat is asked for somewhere, and a lookup takes the specific row before
    /// the general one. The catalogue belongs to the story session; a rename there fails here.
    /// </summary>
    public sealed class StoryReactionsTests
    {
        private static readonly CharacterPresentation.Reaction[] StoryBeats =
        {
            CharacterPresentation.Reaction.Shocked, CharacterPresentation.Reaction.Tearful, CharacterPresentation.Reaction.Embrace,
            CharacterPresentation.Reaction.Furious, CharacterPresentation.Reaction.StormOff,
        };

        [Test]
        public void StoryReactionsTableNamesRealBeats()
        {
            foreach (var row in StoryReactions.Rows)
            {
                if (row.fallout != null)
                    Assert.That(StoryLog.IsFallout(row.fallout), Is.True, row.contentId + " " + row.role + ": " + row.fallout + " is a fallout kind");
                if (row.contentId == StoryReactions.Any)
                {
                    Assert.That(row.role, Is.EqualTo(StoryReactions.Any).Or.EqualTo(StoryReactions.Expelled).Or.EqualTo(StoryReactions.Onlooker),
                        "a row for any beat names no beat's part");
                    Assert.That(row.fallout, Is.Not.Null, "a row for any beat is keyed on a fallout kind");
                    continue;
                }
                int split = row.contentId.IndexOf(':');
                Assert.That(split, Is.GreaterThan(0), row.contentId + " is arc:beat");
                var arc = StoryCatalog.Find(row.contentId.Substring(0, split));
                Assert.That(arc, Is.Not.Null, row.contentId + ": the arc is in the catalogue");
                var beat = arc.Beat(row.contentId.Substring(split + 1));
                Assert.That(beat, Is.Not.Null, row.contentId + ": the beat is in the arc");
                Assert.That(arc.roles.Select(r => r.key), Does.Contain(row.role), row.contentId + ": " + row.role + " is one of the arc's parts");
                if (row.fallout != null)
                    Assert.That(beat.fallout, Is.EqualTo(row.fallout), row.contentId + " writes the fallout its row is keyed on");
                if (row.moment == StoryReactions.Moment.OnFallout)
                    Assert.That(beat.fallout, Is.Not.Null, row.contentId + " is acted at its fallout, so it must write one");
                if (row.ending != null)
                {
                    Assert.That(row.moment, Is.EqualTo(StoryReactions.Moment.OnEnding), row.contentId + ": an ending is acted at the ending");
                    Assert.That(beat.options.Any(o => o.next != null && o.next.Contains("end:" + row.ending)), Is.True,
                        row.contentId + " has an option that ends the arc at end:" + row.ending);
                }
            }
            foreach (var beat in StoryBeats)
                Assert.That(StoryReactions.Rows.Any(row => row.reaction == beat), Is.True, beat + " is asked for by at least one row");
        }

        [Test]
        public void StoryReactionsChooseByRoleThenKind()
        {
            const string blowUp = "kitchen-blowup:blow-up";
            Assert.That(StoryReactions.For(StoryReactions.Moment.OnFallout, blowUp, "HOTHEAD", StoryLog.Blowup),
                Is.EqualTo(CharacterPresentation.Reaction.Furious), "the one who started it is furious");
            Assert.That(StoryReactions.For(StoryReactions.Moment.OnFallout, blowUp, "WITNESS2", StoryLog.Blowup),
                Is.EqualTo(CharacterPresentation.Reaction.Shocked), "a witness the beat names is shocked");
            Assert.That(StoryReactions.For(StoryReactions.Moment.OnFallout, "the-house-turns:turns", "TARGET", StoryLog.Blowup),
                Is.EqualTo(CharacterPresentation.Reaction.StormOff), "the one the house turned on leaves");
            Assert.That(StoryReactions.For(StoryReactions.Moment.OnFallout, "the-house-turns:turns", "AGGRESSOR3", StoryLog.Blowup),
                Is.EqualTo(CharacterPresentation.Reaction.Furious));
            Assert.That(StoryReactions.For(StoryReactions.Moment.OnFallout, "some-arc:some-beat", "ANYONE", StoryLog.Blowup),
                Is.EqualTo(CharacterPresentation.Reaction.Furious), "any other part in a blow-up is furious");
            Assert.That(StoryReactions.For(StoryReactions.Moment.OnFallout, null, StoryReactions.Expelled, StoryLog.Expulsion),
                Is.EqualTo(CharacterPresentation.Reaction.Tearful), "the one removed is in tears");
            Assert.That(StoryReactions.For(StoryReactions.Moment.OnFallout, null, StoryReactions.Onlooker, StoryLog.Expulsion),
                Is.EqualTo(CharacterPresentation.Reaction.Shocked), "the onlookers are shocked");
            Assert.That(StoryReactions.For(StoryReactions.Moment.OnFallout, null, null, StoryLog.Penalty),
                Is.EqualTo(CharacterPresentation.Reaction.Shocked));
            Assert.That(StoryReactions.For(StoryReactions.Moment.OnFallout, null, null, StoryLog.HouseMeeting),
                Is.EqualTo(CharacterPresentation.Reaction.Shocked));
            Assert.That(StoryReactions.For(StoryReactions.Moment.OnSceneArrival, "the-fallout:fallout", "NOMINEE"),
                Is.EqualTo(CharacterPresentation.Reaction.StormOff), "the nominee storms through as they reach the scene");
            Assert.That(StoryReactions.For(StoryReactions.Moment.OnFallout, "the-fallout:fallout", "NOMINEE"), Is.Null,
                "and nothing at a fallout the beat does not write");
            Assert.That(StoryReactions.For(StoryReactions.Moment.OnEnding, "cold-shoulder:silent", "TARGET", null, StoryReactions.Peace),
                Is.EqualTo(CharacterPresentation.Reaction.Embrace), "made up: they embrace");
            Assert.That(StoryReactions.For(StoryReactions.Moment.OnEnding, "cold-shoulder:silent", "TARGET", null, "fizzled"), Is.Null,
                "only at the peace the player saw them make");
            Assert.That(StoryReactions.ActsEnding("kitchen-blowup:words", StoryReactions.Peace), Is.True);
            Assert.That(StoryReactions.ActsEnding("kitchen-blowup:aftermath", "apology"), Is.False);
        }
    }
}
