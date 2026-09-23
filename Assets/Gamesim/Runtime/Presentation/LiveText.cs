using System;
using TMPro;
using UnityEngine;

namespace Gamesim.Presentation
{
    /// <summary>
    /// A label that reads its words from a source every frame, for the one chip on the strip whose
    /// words change while nothing re-renders: the player's own progress while a competition is
    /// played. The HUD rebuilds on renders, and a competition does not render every frame.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LiveText : MonoBehaviour
    {
        public Func<string> Source;
        private TMP_Text label;

        private void Awake() => label = GetComponent<TMP_Text>();

        private void LateUpdate()
        {
            if (label == null || Source == null) return;
            string words = Source();
            if (!string.IsNullOrEmpty(words) && label.text != words) label.text = words;
        }
    }
}
