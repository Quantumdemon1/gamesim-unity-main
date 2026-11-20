using System.Collections.Generic;
using System.Linq;
using Gamesim.Simulation;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// The star pool speaks about the game, not about the speaker (rulings R7 and R15: game register only for the All-Stars). Beyond
    /// the deny-list in <see cref="TalkTablesTests"/>: no star line hints at a personal half of a real person's time on television,
    /// and none hands an All-Star a past misstep of their own as a fact. These phrases were in three talks and were reworded as
    /// general game advice. Simulation only, so the Unity-free subset runs it.
    /// </summary>
    public sealed class StarTalkRegisterTests
    {
        static readonly string[] OffRegister =
        {
            "game half", "about me under", "my first time in. not about", "i'm still learning that",
            "trusted one wrong read the first time", "was it the read, or was it you", "who did you underrate",
        };

        static IEnumerable<string> Spoken(TalkRow row)
        {
            yield return row.Open;
            foreach (var r in row.Replies) { yield return r.Line; yield return r.Lands; yield return r.Even; yield return r.Grates; }
            if (row.Standing != null) { yield return row.Standing.Honest; yield return row.Standing.Neutral; yield return row.Standing.Dishonest; }
        }

        [Test]
        public void NoStarLineHintsAtAPersonalHalfOrAPastMisstepOfItsOwn()
        {
            var star = TalkTables.All.Where(r => r.Pool == TalkPools.Star).ToList();
            Assert.That(star.Count, Is.GreaterThanOrEqualTo(60), "The whole star pool was read (R9).");
            var bad = star.SelectMany(r => Spoken(r).Where(l => l != null)
                    .SelectMany(l => OffRegister.Where(p => l.ToLowerInvariant().Contains(p)).Select(p => r.Id + ": " + p)))
                .ToList();
            Assert.That(bad, Is.Empty);
        }
    }
}
