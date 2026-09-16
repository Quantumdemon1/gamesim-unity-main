using UnityEngine;

namespace Gamesim.Presentation
{
    /// <summary>
    /// A body built for <see cref="CharacterPresentation"/> by something other than the component
    /// itself — today, UMA. Only the two handles the presentation layer actually drives are
    /// exposed; everything else about how the body was assembled stays with the provider.
    /// </summary>
    public readonly struct CharacterBody
    {
        public readonly GameObject Root;
        public readonly Animator Animator;

        /// <summary>
        /// True when the mesh and skeleton arrive over later frames instead of at creation.
        /// The presentation resolves its head bone lazily in that case rather than assuming
        /// a rig exists the instant the body is handed over.
        /// </summary>
        public readonly bool Deferred;

        public CharacterBody(GameObject root, Animator animator, bool deferred)
        {
            Root = root;
            Animator = animator;
            Deferred = deferred;
        }

        public bool Exists => Root != null;
    }

    /// <summary>Supplies character bodies that <see cref="CharacterPresentation"/> cannot build itself.</summary>
    public interface ICharacterBodyProvider
    {
        /// <summary>
        /// Builds a body for <paramref name="appearanceId"/> under <paramref name="parent"/>.
        /// Returning false is normal and means "no body for this persona" — the presentation then
        /// falls back to its authored prefab, and failing that to the primitive rig.
        /// </summary>
        bool TryCreate(string appearanceId, Transform parent, Color wardrobe, out CharacterBody body);

        /// <summary>Re-tints an existing body when a houseguest's palette changes.</summary>
        void SetWardrobeColor(in CharacterBody body, Color wardrobe);
    }

    /// <summary>
    /// The single registration point for a body provider.
    ///
    /// Nothing registers by default. A scene opts in by carrying the component that installs a
    /// provider, which is what keeps code-built test scenes — and any clone of this repository
    /// without the UMA package — on the primitive rig they have always used.
    /// </summary>
    public static class CharacterBodySource
    {
        public static ICharacterBodyProvider Provider { get; private set; }

        public static void Register(ICharacterBodyProvider provider) => Provider = provider;

        public static void Unregister(ICharacterBodyProvider provider)
        {
            if (ReferenceEquals(Provider, provider)) Provider = null;
        }
    }
}
