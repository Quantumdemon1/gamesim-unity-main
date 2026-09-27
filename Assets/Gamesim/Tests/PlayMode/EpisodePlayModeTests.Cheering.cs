using System.Collections;
using System.Linq;
using Gamesim.Presentation;
using Gamesim.Simulation;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace Gamesim.Tests.PlayMode
{
    /// <summary>
    /// The competition reaches the body that won it: the houseguest the standings put first is
    /// asked to cheer the moment the result is committed, whether that houseguest is an NPC or the
    /// player. Asked, and recorded, whichever body is underneath - the authored clip plays only
    /// where the controller declares its trigger, the rule every other reaction follows.
    /// </summary>
    public sealed partial class EpisodePlayModeTests
    {
        [UnityTest]
        public IEnumerator Competition_TheHouseguestWhoWinsIsAskedToCheer()
        {
            string winner = null;
            for (int guard = 0; guard < 400 && winner == null; guard++)
            {
                var before = director.Snapshot;
                if (before.phase == EpisodePhase.Finished) break;
                int known = before.events.Count;
                var result = director.Submit(NextCommand(before));
                yield return null;
                if (!result.accepted) continue;
                // The same event the card is keyed off: a competition the player is entitled to see.
                bool played = result.state.events.Skip(known).Any(entry => entry.kind == "competition"
                    && (entry.audienceIds.Count == 0 || entry.audienceIds.Contains(result.state.playerId)));
                if (!played || result.state.competitionScores.Count == 0) continue;
                // Worked out here from the committed scores rather than from the director, so the
                // test is checking the result and not the director's own reading of it.
                winner = result.state.competitionScores.OrderByDescending(score => score.score).First().contestantId;
            }
            Assert.That(winner, Is.Not.Null, "The season must reach a competition the player is entitled to see.");

            var body = Body(winner);
            Assert.That(body, Is.Not.Null, winner + " has a body in the house");
            Assert.That(body.LastReaction, Is.EqualTo(CharacterPresentation.Reaction.Cheered),
                winner + " won the competition and was asked to cheer");
        }
    }
}
