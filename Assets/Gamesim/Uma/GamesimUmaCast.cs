using Gamesim.Presentation;
using UMA;
using UnityEngine;

namespace Gamesim.Uma
{
    /// <summary>
    /// The switch that puts a scene's houseguests on UMA bodies.
    ///
    /// Nothing about UMA is global: a scene without this component builds the same primitive rig it
    /// always has, which is what keeps the code-built play-mode scenes in the test suite — and any
    /// clone of this repository that has not installed UMA — on their existing behaviour.
    ///
    /// Runs early so the provider is registered before any <see cref="CharacterPresentation"/>
    /// reaches its first Build.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-200)]
    public sealed class GamesimUmaCast : MonoBehaviour
    {
        [Tooltip("Turn off to fall back to the primitive rig without removing the component.")]
        [SerializeField] private bool useUmaBodies = true;

        private UmaBodyProvider provider;

        private void Awake()
        {
            if (!useUmaBodies) return;

            // Touching the generator here pays UMA's one-time scene setup during load rather than
            // in the middle of the first houseguest's build.
            var indexer = UMAAssetIndexer.Instance;
            if (indexer == null)
            {
                Debug.LogWarning("[Gamesim.Uma] No UMA asset index; houseguests will use the primitive rig.");
                return;
            }
            var generator = indexer.Generator;
            if (generator == null)
            {
                Debug.LogWarning("[Gamesim.Uma] UMA could not provide a generator; houseguests will use the primitive rig.");
                return;
            }

            provider = new UmaBodyProvider();
            CharacterBodySource.Register(provider);
        }

        private void OnDestroy()
        {
            if (provider == null) return;
            CharacterBodySource.Unregister(provider);
            provider = null;
        }
    }
}
