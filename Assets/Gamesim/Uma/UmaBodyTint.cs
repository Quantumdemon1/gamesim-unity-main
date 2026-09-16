using System.Collections.Generic;
using UMA;
using UMA.CharacterSystem;
using UnityEngine;

namespace Gamesim.Uma
{
    /// <summary>
    /// Rides along on a UMA body and applies the houseguest's look the moment UMA finishes
    /// assembling it.
    ///
    /// Both jobs here have to wait for the build: the stylize pass needs the generated materials to
    /// exist, and the fabric tint needs the shared colours the wardrobe recipes contribute, neither
    /// of which is available when the body is first created. Keeping that state on the body rather
    /// than in a table beside it means it cannot outlive the character it describes.
    /// </summary>
    [DisallowMultipleComponent]
    internal sealed class UmaBodyTint : MonoBehaviour
    {
        /// <summary>
        /// Shared-colour names a clothing recipe may expose for its main fabric. UMA's shipped
        /// wardrobe is inconsistent about this, so the palette is applied to whichever of these the
        /// assembled recipe actually declares — a recipe with baked-in colour is left alone rather
        /// than tinted at random.
        /// </summary>
        private static readonly string[] FabricColorNames = { "Clothes", "Cloth", "Fabric", "Shirt", "Top", "Outfit" };

        private DynamicCharacterAvatar avatar;
        private Color fabric;
        private Color appliedFabric;
        private bool hasApplied;
        private IReadOnlyDictionary<string, float> proportions;
        private bool proportionsApplied;

        internal void Bind(DynamicCharacterAvatar target, Color wardrobe, IReadOnlyDictionary<string, float> dna)
        {
            avatar = target;
            fabric = wardrobe;
            proportions = dna;
            if (avatar != null) avatar.OnCharacterUpdated += OnCharacterUpdated;
        }

        /// <summary>
        /// Applies the house proportions once the character exists.
        ///
        /// <see cref="DynamicCharacterAvatar.predefinedDNA"/> looks like the right place for this and
        /// is not: UMA's <c>ApplyPredefinedDNA</c> returns immediately for any race with
        /// <c>useNewDNA</c> set, which both human races are, so preloaded values are silently
        /// discarded. Setting the DNA on the built character and marking it dirty is the path that
        /// actually works for these races. The guard matters because that rebuild raises
        /// CharacterUpdated again.
        /// </summary>
        private void ApplyProportions()
        {
            if (proportionsApplied || avatar == null || proportions == null || proportions.Count == 0) return;
            proportionsApplied = true;

            var dna = avatar.GetDNA();
            bool changed = false;
            foreach (var entry in proportions)
            {
                if (!dna.TryGetValue(entry.Key, out var setter)) continue;
                if (Mathf.Approximately(setter.Value, entry.Value)) continue;
                setter.Set(entry.Value);
                changed = true;
            }

            if (changed && avatar.umaData != null) avatar.umaData.Dirty(true, false, true);
        }

        /// <summary>Re-tints a body whose houseguest changed palette. Safe before the build finishes.</summary>
        internal void SetFabric(Color wardrobe)
        {
            fabric = wardrobe;
            Apply();
        }

        private void OnCharacterUpdated(UMAData data)
        {
            ApplyProportions();
            UmaStylizer.Apply(gameObject);
            Apply();
        }

        private void Apply()
        {
            if (avatar == null || avatar.characterColors == null) return;
            if (hasApplied && appliedFabric == fabric) return;

            var colors = avatar.characterColors.Colors;
            if (colors == null) return;

            bool changed = false;
            for (int i = 0; i < colors.Count; i++)
            {
                var declared = colors[i];
                if (declared == null || string.IsNullOrEmpty(declared.name)) continue;
                for (int n = 0; n < FabricColorNames.Length; n++)
                {
                    if (declared.name != FabricColorNames[n]) continue;
                    avatar.SetColor(declared.name, fabric);
                    changed = true;
                    break;
                }
            }

            // Recording the applied value before refreshing matters: UpdateColors drives another
            // CharacterUpdated, and without this the pair would bounce off each other forever.
            appliedFabric = fabric;
            hasApplied = true;
            if (changed) avatar.UpdateColors(true);
        }

        private void OnDestroy()
        {
            if (avatar != null) avatar.OnCharacterUpdated -= OnCharacterUpdated;
            avatar = null;
        }
    }
}
