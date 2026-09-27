using UnityEngine;

namespace Gamesim.Presentation
{
    /// <summary>Present on deferred providers. Ready includes the post-build DNA/color pass.</summary>
    public sealed class CharacterBodyBuildState : MonoBehaviour
    {
        public int Revision { get; set; }
        public bool Ready { get; set; }
        public string Substitution { get; set; }
        /// <summary>The body the provider built, as the moves it strikes need to know it: never read from pronouns.</summary>
        public BodyFrame Frame { get; set; }
    }

    /// <summary>
    /// Which of the two bodies a houseguest was built on, for the poses a body of that build strikes.
    /// It is the body's, not the person's: the creator keeps pronouns apart from appearance on
    /// purpose, so nothing here is ever read from them. Unknown for any body that does not say.
    /// </summary>
    public enum BodyFrame { Unknown, Masculine, Feminine }
}
