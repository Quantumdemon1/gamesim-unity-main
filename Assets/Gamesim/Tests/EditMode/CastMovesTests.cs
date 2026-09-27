using System.Linq;
using Gamesim.Presentation;
using Gamesim.Simulation;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// Which moves a houseguest makes when the show asks them to be themselves: a body strikes the
    /// poses captured on its own build, never the foot on a ledge or the look away for a camera,
    /// neighbours at the door strike different ones, and the kind of player a card describes picks
    /// the dance.
    /// </summary>
    public sealed class CastMovesTests
    {
        private static readonly CharacterPresentation.Pose[] CapturedOnAMan =
        {
            CharacterPresentation.Pose.HandBehindHead, CharacterPresentation.Pose.FootUp,
            CharacterPresentation.Pose.OverShoulder, CharacterPresentation.Pose.AtEase,
        };

        private static readonly CharacterPresentation.Pose[] CapturedOnAWoman =
        {
            CharacterPresentation.Pose.HandOnHip, CharacterPresentation.Pose.HandOnHipGlance, CharacterPresentation.Pose.PowerStance,
        };

        [Test]
        public void ABodyStrikesThePosesCapturedOnItsOwnBuild()
        {
            Assert.That(CastMoves.ForCamera(BodyFrame.Masculine), Is.SubsetOf(CapturedOnAMan), "a man's body strikes a man's poses");
            Assert.That(CastMoves.ForCamera(BodyFrame.Feminine), Is.SubsetOf(CapturedOnAWoman), "a woman's body strikes a woman's poses");
            Assert.That(CastMoves.ForCamera(BodyFrame.Masculine), Is.Not.Empty);
            Assert.That(CastMoves.ForCamera(BodyFrame.Feminine), Is.Not.Empty);
            foreach (var frame in new[] { BodyFrame.Masculine, BodyFrame.Feminine, BodyFrame.Unknown })
            {
                Assert.That(CastMoves.ForCamera(frame), Has.No.Member(CharacterPresentation.Pose.FootUp),
                    "the foot on a ledge has no ledge on the floor in front of a camera");
                Assert.That(CastMoves.ForCamera(frame), Has.No.Member(CharacterPresentation.Pose.OverShoulder),
                    "the look over the shoulder looks away from the lens");
                foreach (var id in CastTemplates.In(CastTemplates.Roster.Regular).Select(t => t.Id))
                    foreach (var avoid in CastMoves.ForCamera(frame))
                        Assert.That(CastMoves.ForCamera(frame), Has.Member(CastMoves.PoseFor(frame, id, avoid)),
                            id + " strikes one of the " + frame + " poses");
            }
        }

        [Test]
        public void EachPersonOwnsAPoseAndTheNextThroughTheDoorStrikesAnother()
        {
            foreach (var frame in new[] { BodyFrame.Masculine, BodyFrame.Feminine })
            {
                var ids = CastTemplates.In(CastTemplates.Roster.Regular).Concat(CastTemplates.In(CastTemplates.Roster.AllStars))
                    .Select(t => t.Id).ToArray();
                foreach (var id in ids)
                {
                    var own = CastMoves.PoseFor(frame, id);
                    Assert.That(CastMoves.PoseFor(frame, id), Is.EqualTo(own),
                        id + " strikes the same pose every time: presentation, never the season's generator");
                    Assert.That(CastMoves.PoseFor(frame, id, own), Is.Not.EqualTo(own),
                        id + " strikes another after somebody who struck theirs");
                    foreach (var other in CastMoves.ForCamera(frame).Where(pose => pose != own))
                        Assert.That(CastMoves.PoseFor(frame, id, other), Is.EqualTo(own), id + " keeps their own when it is free");
                }
                Assert.That(ids.Select(id => CastMoves.PoseFor(frame, id)).Distinct().Count(), Is.EqualTo(CastMoves.ForCamera(frame).Count),
                    "across the whole cast, every " + frame + " pose is somebody's");
            }
        }

        [Test]
        public void TheKindOfPlayerACardDescribesPicksTheDance()
        {
            foreach (var template in CastTemplates.In(CastTemplates.Roster.Regular).Concat(CastTemplates.In(CastTemplates.Roster.AllStars)))
            {
                var who = CastTemplates.ToContestant(template, false);
                var expected = template.Category == "Socialite" ? CharacterPresentation.DanceStyle.Samba
                    : template.Category == "Wildcard" ? CharacterPresentation.DanceStyle.Wave
                    : template.Category == "Competitor" ? CharacterPresentation.DanceStyle.HipHop
                    : CharacterPresentation.DanceStyle.House;
                Assert.That(CastMoves.DanceFor(who), Is.EqualTo(expected), template.Name + " (" + template.Category + ")");
                bool dances = template.Category == "Socialite" || template.Category == "Wildcard"
                    || template.Archetype.ToLowerInvariant().Contains("party");
                Assert.That(CastMoves.DancesIn(who), Is.EqualTo(dances),
                    template.Name + (dances ? " dances onto the mark" : " strikes a pose on the mark"));
            }
            var partyAnimal = CastTemplates.ToContestant(CastTemplates.Find("casey-wilson"), false);
            Assert.That(CastMoves.DancesIn(partyAnimal), Is.True, "the party animal dances in");
            Assert.That(CastMoves.DancesIn(null), Is.False, "nobody is nobody's dancer");
        }
    }
}
