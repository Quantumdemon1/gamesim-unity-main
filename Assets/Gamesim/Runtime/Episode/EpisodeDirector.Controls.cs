using Gamesim.House;

namespace Gamesim.Episode
{
    /// <summary>
    /// The director's side of PLAN A's input slices: which actions it reads when it has no rig.
    /// </summary>
    public sealed partial class EpisodeDirector
    {
        /// <summary>The house's actions: the rig's, or the shared copy when there is no rig (HouseInput).</summary>
        private HouseCameraActions HouseActions => cameraRig != null ? cameraRig.Actions : HouseInput.Actions;
    }
}
