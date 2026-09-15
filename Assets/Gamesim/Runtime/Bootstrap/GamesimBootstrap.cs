using Gamesim.Core;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Gamesim.Bootstrap
{
    /// <summary>
    /// Persistent entry point for services added in later vertical slices.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GamesimBootstrap : MonoBehaviour
    {
        public static GamesimBootstrap Instance { get; private set; }

        public string FoundationVersion => FoundationInfo.FoundationVersion;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        private void Start()
        {
            if (Instance == this && SceneManager.GetActiveScene().path == FoundationInfo.BootstrapScenePath)
            {
                var destination = Application.CanStreamedLevelBeLoaded("EpisodeHouse") ? "EpisodeHouse" : "HousePrototype";
                if (Application.CanStreamedLevelBeLoaded(destination))
                    SceneManager.LoadSceneAsync(destination, LoadSceneMode.Single);
            }
        }
    }
}
