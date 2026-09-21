using System.Collections.Generic;
using Gamesim.Simulation;
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
        public readonly ICharacterBodyProvider Owner;

        /// <summary>
        /// True when the mesh and skeleton arrive over later frames instead of at creation.
        /// The presentation resolves its head bone lazily in that case rather than assuming
        /// a rig exists the instant the body is handed over.
        /// </summary>
        public readonly bool Deferred;

        public CharacterBody(GameObject root, Animator animator, bool deferred, ICharacterBodyProvider owner = null)
        {
            Root = root;
            Animator = animator;
            Deferred = deferred;
            Owner = owner;
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

    public enum CharacterBuildPurpose { Gameplay, Studio, Portrait }

    /// <summary>An explicit request: preview bodies do not need a fake contestant hierarchy.</summary>
    public readonly struct CharacterBodyRequest
    {
        public readonly string ContestantId, FallbackId;
        public readonly CharacterAppearance Appearance;
        public readonly CharacterBuildPurpose Purpose;
        public readonly int Revision;
        public CharacterBodyRequest(string contestantId, string fallbackId, CharacterAppearance appearance,
            CharacterBuildPurpose purpose = CharacterBuildPurpose.Gameplay, int revision = 0)
        {
            ContestantId = contestantId; FallbackId = fallbackId; Appearance = appearance?.Clone();
            Purpose = purpose; Revision = revision;
        }
    }

    public interface IModularCharacterBodyProvider : ICharacterBodyProvider
    {
        ICharacterAppearanceCatalog Catalog { get; }
        bool TryCreate(in CharacterBodyRequest request, Transform parent, Color badgeColor, out CharacterBody body);
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
        private static readonly List<ICharacterBodyProvider> Providers = new List<ICharacterBodyProvider>();
        public static ICharacterBodyProvider Provider => Providers.Count == 0 ? null : Providers[Providers.Count - 1];

        public static void Register(ICharacterBodyProvider provider)
        {
            if (provider != null && !Providers.Contains(provider)) Providers.Add(provider);
        }

        public static void Unregister(ICharacterBodyProvider provider)
        {
            Providers.Remove(provider);
        }

        public static bool TryCreate(in CharacterBodyRequest request, Transform parent, Color badgeColor,
            out CharacterBody body)
        {
            body = default;
            for (int i = Providers.Count - 1; i >= 0; i--)
            {
                var provider = Providers[i];
                bool created = provider is IModularCharacterBodyProvider modular
                    ? modular.TryCreate(request, parent, badgeColor, out body)
                    : provider.TryCreate(request.FallbackId, parent, badgeColor, out body);
                if (!created || !body.Exists) continue;
                body = new CharacterBody(body.Root, body.Animator, body.Deferred, provider);
                return true;
            }
            return false;
        }
    }
}
