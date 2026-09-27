using System;
using System.Collections.Generic;
using UnityEngine;

namespace Gamesim.Uma
{
    /// <summary>Content-owned IDs survive UMA asset renames. Keep former names in aliases when renaming a recipe.</summary>
    [CreateAssetMenu(menuName = "Gamesim/Characters/UMA wardrobe catalog")]
    public sealed class UmaWardrobeCatalog : ScriptableObject
    {
        public int version = 1;
        public List<UmaWardrobeEntry> entries = new List<UmaWardrobeEntry>();

#if UNITY_EDITOR
        private void OnValidate()
        {
            foreach (var entry in entries)
            {
                if (entry == null) continue;
                if (string.IsNullOrEmpty(entry.id)) entry.id = "uma-" + Guid.NewGuid().ToString("N");
                if (string.IsNullOrWhiteSpace(entry.recipeName)) continue;
                entry.aliases = entry.aliases ?? new List<string>();
                if (!entry.aliases.Contains(entry.recipeName)) entry.aliases.Add(entry.recipeName);
            }
        }
#endif
    }

    [Serializable]
    public sealed class UmaWardrobeEntry
    {
        [Tooltip("Permanent save identifier. Do not change after release.")]
        public string id;
        public string recipeName;
        public List<string> aliases = new List<string>();
        public string label;
        [Tooltip("Equivalent garment family across bodies, for example top.tshirt or shoes.trainer.low.")]
        public string styleGroup;
        public List<string> tags = new List<string>();
        public int fallbackPriority = 100;
        public bool available = true;
    }
}
