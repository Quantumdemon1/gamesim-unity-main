using Gamesim.Episode;
using Gamesim.Presentation;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// How the house answers your moves: by what your own record says each houseguest is to you -
    /// the reading the cast strip and the notebook show - and never by what they privately think.
    /// </summary>
    public sealed class EmoteAnswerTests
    {
        [Test]
        public void FriendsCheerYouOnRivalsShrugAndEveryoneElseLooks()
        {
            foreach (var move in new[] { EpisodeDirector.Emote.Cheer, EpisodeDirector.Emote.Celebrate,
                         EpisodeDirector.Emote.Samba, EpisodeDirector.Emote.HipHop, EpisodeDirector.Emote.Wave })
            {
                Assert.That(EpisodeDirector.AnswerTo(move, RelationshipWeb.Kind.Friendship), Is.EqualTo(EpisodeDirector.Answer.Cheer), move + ": a friend");
                Assert.That(EpisodeDirector.AnswerTo(move, RelationshipWeb.Kind.Alliance), Is.EqualTo(EpisodeDirector.Answer.Cheer), move + ": an ally");
                Assert.That(EpisodeDirector.AnswerTo(move, RelationshipWeb.Kind.Distrust), Is.EqualTo(EpisodeDirector.Answer.Shrug), move + ": somebody you distrust");
                Assert.That(EpisodeDirector.AnswerTo(move, RelationshipWeb.Kind.Rivalry), Is.EqualTo(EpisodeDirector.Answer.Shrug), move + ": a rival");
                Assert.That(EpisodeDirector.AnswerTo(move, RelationshipWeb.Kind.Neutral), Is.EqualTo(EpisodeDirector.Answer.Look), move + ": anyone else");
            }
            foreach (RelationshipWeb.Kind kind in System.Enum.GetValues(typeof(RelationshipWeb.Kind)))
            {
                Assert.That(EpisodeDirector.AnswerTo(EpisodeDirector.Emote.Pose, kind), Is.EqualTo(EpisodeDirector.Answer.Look), "a pose is looked at");
                Assert.That(EpisodeDirector.AnswerTo(EpisodeDirector.Emote.Shrug, kind), Is.EqualTo(EpisodeDirector.Answer.None), "a shrug is nobody's business");
            }
        }
    }
}
