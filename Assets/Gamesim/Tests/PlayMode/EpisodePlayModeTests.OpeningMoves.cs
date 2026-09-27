using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Gamesim.House;
using Gamesim.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Gamesim.Tests.PlayMode
{
    public sealed partial class EpisodePlayModeTests
    {
        /// <summary>
        /// On the mark, as the name comes up, the dancers dance their own dance and everyone else
        /// strikes a pose a body of their build strikes - and two people revealed one after the
        /// other never strike the same pose. It all ends with the show.
        /// </summary>
        [UnityTest]
        public IEnumerator OpeningStage_EveryoneOnTheMarkMakesTheirOwnMove()
        {
            var opening = director.Opening;
            var state = director.Snapshot;
            var visuals = SceneComponents<HouseNpc>().ToDictionary(npc => npc.Id, npc => npc.GetComponent<CharacterPresentation>());
            visuals[state.playerId] = player.GetComponent<CharacterPresentation>();
            director.PlayOpeningForVerification(stage: true, holdHeadless: true);
            yield return WaitFor(() => director.IsOpeningStaged, 40f, "The house is placed behind the front door once every body is built.");

            // Who made which move, in the order they made it.
            var moves = new List<(string id, CharacterPresentation.BodyActivity activity, CharacterPresentation.Pose pose,
                CharacterPresentation.DanceStyle dance, BodyFrame frame)>();
            float until = Time.realtimeSinceStartup + 80f;
            while (moves.Count < 4 && Time.realtimeSinceStartup < until && opening.IsPlaying)
            {
                foreach (var (id, visual) in visuals)
                    if (visual != null && moves.All(move => move.id != id)
                        && (visual.Activity == CharacterPresentation.BodyActivity.Dancing || visual.Activity == CharacterPresentation.BodyActivity.Posing))
                        moves.Add((id, visual.Activity, visual.HeldPose, visual.Dance, visual.Frame));
                yield return null;
            }
            Assert.That(moves.Count, Is.EqualTo(4), "The player and the first three houseguests each made a move on the mark.");
            Assert.That(moves[0].id, Is.EqualTo(state.playerId), "The player is revealed first.");

            for (int i = 0; i < moves.Count; i++)
            {
                var (id, activity, pose, dance, frame) = moves[i];
                var who = state.Find(id);
                Assert.That(frame, Is.Not.EqualTo(BodyFrame.Unknown), id + "'s body says which build it is.");
                if (CastMoves.DancesIn(who))
                {
                    Assert.That(activity, Is.EqualTo(CharacterPresentation.BodyActivity.Dancing), id + " dances onto the mark.");
                    Assert.That(dance, Is.EqualTo(CastMoves.DanceFor(who)), id + " dances their own dance.");
                }
                else
                {
                    Assert.That(activity, Is.EqualTo(CharacterPresentation.BodyActivity.Posing), id + " strikes a pose on the mark.");
                    Assert.That(CastMoves.ForCamera(frame), Has.Member(pose), id + " strikes a pose a " + frame + " body strikes for the lens.");
                }
                if (i > 0 && activity == CharacterPresentation.BodyActivity.Posing
                    && moves[i - 1].activity == CharacterPresentation.BodyActivity.Posing)
                    Assert.That(pose, Is.Not.EqualTo(moves[i - 1].pose), id + " strikes another pose than the person before them.");
            }
            Assert.That(moves.Any(move => move.activity == CharacterPresentation.BodyActivity.Dancing)
                        && moves.Any(move => move.activity == CharacterPresentation.BodyActivity.Posing), Is.True,
                "The fixture's first four through the door include a dancer and a poser.");

            opening.Skip();
            yield return WaitFor(() => !opening.IsPlaying, 10f, "The opening ends when it is skipped.");
            yield return Frames(3);
            foreach (var (id, visual) in visuals)
                Assert.That(visual.Activity, Is.Not.EqualTo(CharacterPresentation.BodyActivity.Dancing)
                    .And.Not.EqualTo(CharacterPresentation.BodyActivity.Posing), id + "'s move ended with the show.");
        }
    }
}
