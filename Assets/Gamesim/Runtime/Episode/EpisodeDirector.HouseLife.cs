using System.Linq;
using Gamesim.House;
using UnityEngine;

namespace Gamesim.Episode
{
    /// <summary>
    /// The house answering what the player does in it. For now, one thing: the room goes dim
    /// while the player sleeps in it.
    /// </summary>
    public sealed partial class EpisodeDirector
    {
        /// <summary>How far the room's fill light drops for a sleeper, and how long it takes to get there.</summary>
        public const float SleepDimTo = .35f, SleepDimSeconds = 1.5f;

        private Light sleepLight;
        private float sleepLightBase, sleepDim;

        /// <summary>The fill light a sleeping player has dimmed, while it is dimmed; null otherwise.</summary>
        public Light DimmedForSleep => sleepLight;

        /// <summary>
        /// Dims the fill light of the room the player is asleep in, and brings it back when they
        /// get up. The fills are Mixed lights under an indirect-only bake, so this takes down the
        /// direct light and leaves the baked bounce where it was: the room is dimmer, not dark.
        /// The light's own value is kept and put back exactly.
        /// </summary>
        private void TickSleepLight()
        {
            var seat = player != null ? player.GetComponent<HouseSeatPresentation>() : null;
            bool asleep = IsPlayerHouseActivityActive && playerActivityKind == HouseFurnitureActivity.Sleep
                && seat != null && seat.Settled && seat.Mode == HouseAnchorPose.Lie;
            if (asleep && sleepLight == null && playerActivity?.Anchor != null)
            {
                string name = playerActivity.Anchor.RoomId + " fill";
                sleepLight = gameObject.scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Light>())
                    .FirstOrDefault(light => light.name == name && light.isActiveAndEnabled);
                if (sleepLight != null) { sleepLightBase = sleepLight.intensity; sleepDim = 0f; }
            }
            if (sleepLight == null) return;
            bool still = cameraRig != null && cameraRig.ReducedMotion;
            sleepDim = still ? (asleep ? 1f : 0f)
                : Mathf.MoveTowards(sleepDim, asleep ? 1f : 0f, Time.unscaledDeltaTime / SleepDimSeconds);
            sleepLight.intensity = sleepLightBase * Mathf.Lerp(1f, SleepDimTo, Mathf.SmoothStep(0f, 1f, sleepDim));
            if (!asleep && sleepDim <= 0f) { sleepLight.intensity = sleepLightBase; sleepLight = null; }
        }
    }
}
