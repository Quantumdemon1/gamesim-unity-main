using System.Collections;
using UnityEngine;

namespace Gamesim.Uma.Editor
{
    /// <summary>
    /// Hosts the face-fit export's coroutine when it is started from the menu (UMA builds a body over several frames,
    /// so the export runs in play mode as a coroutine). A test runs the same coroutine itself and needs no host.
    /// Lives on a scene object the menu creates and destroys; never saved.
    /// </summary>
    [AddComponentMenu("")]
    internal sealed class UmaFaceFitRunner : MonoBehaviour
    {
        public IEnumerator Hold(IEnumerator run)
        {
            yield return run;
            if (this != null) Destroy(gameObject);
        }
    }
}
