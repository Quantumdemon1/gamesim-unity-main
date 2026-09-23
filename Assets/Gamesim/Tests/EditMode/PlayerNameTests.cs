using Gamesim.Presentation;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// The player's name in a list: "(You)" after it, once. A player who keeps the default name is
    /// already "You", and the competitors' list read "You (You)".
    /// </summary>
    public sealed class PlayerNameTests
    {
        [Test]
        public void WithYou_MarksThePlayerOnce()
        {
            Assert.That(HudPrimitives.WithYou("Dana Reyes", true), Is.EqualTo("Dana Reyes (You)"));
            Assert.That(HudPrimitives.WithYou("Dana Reyes", true, "  "), Is.EqualTo("Dana Reyes  (You)"));
            Assert.That(HudPrimitives.WithYou("You", true), Is.EqualTo("You"), "The default name is already the mark.");
            Assert.That(HudPrimitives.WithYou(null, true), Is.EqualTo("You"));
            Assert.That(HudPrimitives.WithYou("Maya Hassan", false), Is.EqualTo("Maya Hassan"), "Nobody else is marked.");
        }
    }
}
