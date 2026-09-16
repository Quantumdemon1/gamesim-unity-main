using System.Collections.Generic;
using Gamesim.Presentation;
using UMA;
using UMA.CharacterSystem;
using UnityEngine;

namespace Gamesim.Uma
{
    /// <summary>
    /// Builds houseguest bodies from UMA instead of the primitive rig.
    ///
    /// UMA assembles a character over several frames — it has to atlas the overlays and combine the
    /// meshes — so every body handed back is marked deferred and the presentation layer waits for
    /// the skeleton rather than assuming one. Nothing here reaches back into the presentation; the
    /// only contract is <see cref="ICharacterBodyProvider"/>.
    /// </summary>
    public sealed class UmaBodyProvider : ICharacterBodyProvider
    {
        private const string LocomotionController = "Locomotion";

        // Shared-colour names on the human base recipes.
        private const string SkinColor = "Skin";
        private const string HairColor = "Hair";
        private const string BrowsColor = "Brows";
        private const string EyesColor = "Eyes";

        private readonly HashSet<string> reported = new HashSet<string>();

        public bool TryCreate(string appearanceId, Transform parent, Color wardrobe, out CharacterBody body)
        {
            body = default;
            if (parent == null || !UmaCastLibrary.TryGet(appearanceId, out var look)) return false;

            var indexer = UMAAssetIndexer.Instance;
            if (indexer == null || indexer.GetRace(look.Race) == null)
            {
                ReportOnce(look.Race, "race '" + look.Race + "' is not in the UMA Global Library; " +
                    "open UMA > Global Library and rebuild the index");
                return false;
            }

            var root = new GameObject("UMA Body");
            root.transform.SetParent(parent, false);
            root.transform.localPosition = Vector3.zero;
            root.transform.localRotation = Quaternion.identity;

            // UMA reuses an Animator already on the object, so adding it first keeps the handle we
            // return valid from this frame rather than from whenever UMA gets to the build.
            var animator = root.AddComponent<Animator>();
            animator.applyRootMotion = false;
            var controller = indexer.GetAsset<RuntimeAnimatorController>(LocomotionController);
            if (controller != null) animator.runtimeAnimatorController = controller;
            else ReportOnce(LocomotionController, "animator controller '" + LocomotionController + "' was not found; UMA bodies will not walk");

            var avatar = root.AddComponent<DynamicCharacterAvatar>();
            avatar.activeRace.name = look.Race;
            // UMA declares this inside #if UNITY_EDITOR, so referencing it unguarded compiles in the
            // editor and then fails the player build. Bodies are generated at runtime either way;
            // this only stops UMA also building one into an edit-mode scene.
#if UNITY_EDITOR
            avatar.editorTimeGeneration = false;
#endif
            avatar.BuildCharacterEnabled = true;
            avatar.raceAnimationControllers.defaultAnimationController = controller;

            // Everything this houseguest wears is named in the cast library; UMA's own defaults
            // would quietly re-dress them.
            avatar.preloadWardrobeRecipes.loadDefaultRecipes = false;
            avatar.preloadWardrobeRecipes.recipes.Clear();
            foreach (var recipeName in look.Wardrobe)
            {
                var recipe = indexer.GetAsset<UMAWardrobeRecipe>(recipeName);
                if (recipe == null)
                {
                    ReportOnce(recipeName, "wardrobe recipe '" + recipeName + "' was not found; " +
                        appearanceId + " will go without it");
                    continue;
                }
                avatar.preloadWardrobeRecipes.recipes.Add(new DynamicCharacterAvatar.WardrobeRecipeListItem(recipe));
            }

            avatar.SetColor(SkinColor, look.Skin);
            avatar.SetColor(HairColor, look.Hair);
            avatar.SetColor(BrowsColor, look.Brows);
            avatar.SetColor(EyesColor, look.Eyes);

            // The stylize pass, the fabric tint and the house proportions all need the assembled
            // character, so they are handed to a component that lives on the body and waits for it.
            // House proportions first, then whatever this houseguest overrides.
            var proportions = new Dictionary<string, float>(UmaCastLibrary.HouseProportions);
            foreach (var entry in look.Dna) proportions[entry.Key] = entry.Value;
            root.AddComponent<UmaBodyTint>().Bind(avatar, wardrobe, proportions);

            body = new CharacterBody(root, animator, deferred: true);
            return true;
        }

        public void SetWardrobeColor(in CharacterBody body, Color wardrobe)
        {
            if (!body.Exists) return;
            var tint = body.Root.GetComponent<UmaBodyTint>();
            if (tint != null) tint.SetFabric(wardrobe);
        }

        private void ReportOnce(string key, string message)
        {
            if (!reported.Add(key)) return;
            Debug.LogWarning("[Gamesim.Uma] " + message);
        }
    }
}
