using System.Collections.Generic;
using Gamesim.Simulation;
using UnityEngine;

namespace Gamesim.Presentation
{
    /// <summary>Only one thumbnail avatar builds at once, independent of house actors and lighting.</summary>
    public sealed class CharacterPortraitQueue : MonoBehaviour
    {
        private readonly Queue<KeyValuePair<string, CharacterAppearance>> pending = new Queue<KeyValuePair<string, CharacterAppearance>>();
        private readonly HashSet<string> requested = new HashSet<string>();
        private CharacterStudioPreview preview;
        private string currentKey, currentContent;
        private float startedAt;

        public void Enqueue(string key, CharacterAppearance appearance)
        {
            if (!requested.Add(key)) return;
            pending.Enqueue(new KeyValuePair<string, CharacterAppearance>(key, appearance.Clone()));
        }

        private void Update()
        {
            if (currentKey != null)
            {
                if (preview != null && !preview.IsBuilding && preview.CompletedKey == currentContent)
                {
                    // A failed build is not a face: nothing is kept, and the look is tried again after
                    // a back-off, on a fresh provider build.
                    if (preview.CanRetry) { CharacterPortraits.MarkFailed(currentKey); Destroy(preview.gameObject); preview = null; }
                    else CharacterPortraits.StoreAppearance(currentKey, preview.Texture);
                    requested.Remove(currentKey);
                    currentKey = null;
                }
                else if (Time.unscaledTime - startedAt > 30f)
                {
                    // A failed content build must not starve the entire cast queue.
                    CharacterPortraits.MarkFailed(currentKey);
                    requested.Remove(currentKey);
                    currentKey = null;
                    if (preview != null) Destroy(preview.gameObject);
                    preview = null;
                }
                else return;
            }
            if (pending.Count == 0) return;
            if (preview == null) { preview = CharacterStudioPreview.Create("Portrait studio"); preview.FocusFace(true); }
            var next = pending.Dequeue();
            currentKey = next.Key; currentContent = next.Value.ContentKey(); startedAt = Time.unscaledTime;
            preview.Show(next.Value);
        }

        private void OnDestroy()
        { if (preview != null) Destroy(preview.gameObject); }
    }
}
