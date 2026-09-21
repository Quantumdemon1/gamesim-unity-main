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
        private CharacterAppearance currentAppearance;
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
                    CharacterPortraits.StoreAppearance(currentKey, preview.Texture, preview.CanRetry);
                    // A failed request must get a fresh provider build on its next backoff attempt.
                    if (preview.CanRetry) { Destroy(preview.gameObject); preview = null; }
                    requested.Remove(currentKey);
                    currentKey = null;
                }
                else if (Time.unscaledTime - startedAt > 30f)
                {
                    // A failed content build must not starve the entire cast queue.
                    var template = CastTemplates.Find(currentAppearance?.presetId);
                    string fallback = template == null ? currentAppearance?.fallbackId
                        : CharacterPresentation.AppearanceId(CastTemplates.ToContestant(template, false), template.Id);
                    CharacterPortraits.StoreAppearance(currentKey, CharacterPortraits.Get(fallback)
                        ?? CharacterPortraits.Get("player"), true);
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
            currentKey = next.Key; currentAppearance = next.Value; currentContent = next.Value.ContentKey(); startedAt = Time.unscaledTime;
            preview.Show(next.Value);
        }

        private void OnDestroy()
        { if (preview != null) Destroy(preview.gameObject); }
    }
}
