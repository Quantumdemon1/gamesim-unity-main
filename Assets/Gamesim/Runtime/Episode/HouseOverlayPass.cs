using UnityEngine;

namespace Gamesim.Episode
{
    /// <summary>
    /// The director's late look at what the house draws over itself (UI-UX-PASS-PLAN H0): run after
    /// the camera rig has moved for the frame (its LateUpdate, at the default order) and the
    /// houseguests have placed their name plates (HouseNpc, 210), so a plate is measured where it
    /// is drawn. Measured in Update, a pan left a plate under a card's edge for a frame.
    ///
    /// <para>A component of its own, in a file of its own, because the order is read from the
    /// script: a class nested in another file does not get its DefaultExecutionOrder.</para>
    /// </summary>
    [DisallowMultipleComponent, DefaultExecutionOrder(1000)]
    public sealed class HouseOverlayPass : MonoBehaviour
    {
        /// <summary>What the pass does each frame; the director sets it.</summary>
        public System.Action Late;

        private void LateUpdate() => Late?.Invoke();
    }
}
