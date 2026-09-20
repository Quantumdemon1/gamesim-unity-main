using System;
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
    ///
    /// <para>The body is handed the houseguest controller rather than UMA's <c>Locomotion</c>, so
    /// it sits, talks, argues and reacts on the same parameter names the Generic cast uses. See
    /// <see cref="ResolveController"/> for the one thing that controller cannot carry itself.</para>
    /// </summary>
    public sealed class UmaBodyProvider : ICharacterBodyProvider
    {
        private const string LocomotionController = "Locomotion";

        /// <summary>
        /// The houseguest controller, reached through Resources because it lives in Art/Characters
        /// beside the Generic cast's and nothing in a build would otherwise pull it in. The asset
        /// there is an override controller with no overrides, whose only job is to carry the real
        /// one; <c>HumanoidClipWiring</c> in Gamesim.Editor builds and maintains both.
        /// </summary>
        private const string CastController = "Animation/GamesimHumanoid";

        /// <summary>
        /// The two takes the Humanoid controller has no clip for. The twelve mocap takes have no
        /// standing idle and no walk, and UMA's own are not referenceable: UMA is not in Git, so a
        /// committed controller that named one by GUID would break every clone without it. They are
        /// wired to two takes no cue reaches and swapped here, at runtime, for the idle and run that
        /// UMA's Locomotion controller — resolved by name, like everything else UMA — already holds.
        /// Kept in step with <c>HumanoidClipWiring.IdleStandIn</c> and <c>WalkStandIn</c>.
        /// </summary>
        private const string IdleStandIn = "Sleep_loop";
        private const string WalkStandIn = "SleepLying_loop";

        // Built once and shared: every body wants the same graph over the same two borrowed clips.
        private RuntimeAnimatorController cast;
        private bool castResolved;

        // Shared-colour names on the human base recipes.
        private const string SkinColor = "Skin";
        private const string HairColor = "Hair";
        private const string BrowsColor = "Brows";
        private const string EyesColor = "Eyes";

        private readonly HashSet<string> reported = new HashSet<string>();

        public bool TryCreate(string appearanceId, Transform parent, Color wardrobe, out CharacterBody body)
        {
            body = default;
            if (parent == null) return false;

            // Their own id first, the appearance second. The library holds a look per template, and
            // the appearance is only ever one of the handful of primitive-rig recipes — asking it
            // alone would put five faces on twenty-four people.
            var presentation = parent.GetComponentInParent<CharacterPresentation>();
            if (!UmaCastLibrary.Resolve(presentation == null ? null : presentation.CharacterId,
                    appearanceId, out var look)) return false;

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
            var controller = ResolveController(indexer);
            if (controller != null) animator.runtimeAnimatorController = controller;

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

            // The face. Added here rather than after the build because UMA hands the expression
            // player the race's pose set during the avatar's own Start, and only to a player that
            // is already on the object.
            root.AddComponent<UmaExpressions>();

            body = new CharacterBody(root, animator, deferred: true);
            return true;
        }

        public void SetWardrobeColor(in CharacterBody body, Color wardrobe)
        {
            if (!body.Exists) return;
            var tint = body.Root.GetComponent<UmaBodyTint>();
            if (tint != null) tint.SetFabric(wardrobe);
        }

        /// <summary>
        /// The controller a UMA body is handed: ours, with UMA's idle and run dropped into the two
        /// states the mocap takes could not fill.
        ///
        /// <para>Every step down from that keeps the bodies walking, because walking is the most
        /// visible thing they do and losing it to gain sitting would be a bad trade. If the
        /// houseguest controller is not in Resources, or UMA's Locomotion has no idle and run to
        /// lend, the body gets Locomotion itself — which is where it was before the mocap takes
        /// arrived: it walks, and every other cue falls on the floor.</para>
        /// </summary>
        private RuntimeAnimatorController ResolveController(UMAAssetIndexer indexer)
        {
            if (castResolved) return cast;
            castResolved = true;

            var locomotion = indexer.GetAsset<RuntimeAnimatorController>(LocomotionController);
            if (locomotion == null)
                ReportOnce(LocomotionController, "animator controller '" + LocomotionController +
                    "' was not found; UMA bodies will not walk");

            // The handle carries the controller; unwrap it rather than layering an override on an
            // override, which is a shape Unity has never been happy about.
            var handle = Resources.Load<AnimatorOverrideController>(CastController);
            var houseguest = handle == null ? null : handle.runtimeAnimatorController;
            if (houseguest == null)
            {
                ReportOnce(CastController, "the houseguest controller was not found at Resources/" +
                    CastController + "; run Gamesim > U07 > Wire the Humanoid takes. Until then UMA " +
                    "bodies walk and nothing else.");
                return cast = locomotion;
            }

            var standing = Borrow(locomotion, "Idle");
            var walking = Borrow(locomotion, "Walk", "Run");
            if (standing == null || walking == null || standing == walking)
            {
                ReportOnce("locomotion-clips", "UMA's '" + LocomotionController + "' controller has no " +
                    "standing idle and walk to lend; UMA bodies walk and nothing else.");
                return cast = locomotion;
            }
            if (!Holds(houseguest, IdleStandIn) || !Holds(houseguest, WalkStandIn))
            {
                ReportOnce("stand-ins", "the houseguest controller has no " + IdleStandIn + " and " +
                    WalkStandIn + " to stand in for its idle and walk; re-run Gamesim > U07 > Wire the " +
                    "Humanoid takes. Until then UMA bodies walk and nothing else.");
                return cast = locomotion;
            }

            var overrides = new AnimatorOverrideController(houseguest) { name = "GamesimHumanoid (UMA locomotion)" };
            overrides[IdleStandIn] = standing;
            overrides[WalkStandIn] = walking;
            return cast = overrides;
        }

        /// <summary>A clip off another controller, by name: exactly first, then by prefix.</summary>
        private static AnimationClip Borrow(RuntimeAnimatorController from, params string[] names)
        {
            if (from == null) return null;
            var clips = from.animationClips;
            foreach (var name in names)
                foreach (var clip in clips)
                    if (clip != null && string.Equals(clip.name, name, StringComparison.OrdinalIgnoreCase))
                        return clip;
            foreach (var name in names)
                foreach (var clip in clips)
                    if (clip != null && clip.name.StartsWith(name, StringComparison.OrdinalIgnoreCase))
                        return clip;
            return null;
        }

        private static bool Holds(RuntimeAnimatorController controller, string clipName)
        {
            foreach (var clip in controller.animationClips)
                if (clip != null && clip.name == clipName) return true;
            return false;
        }

        private void ReportOnce(string key, string message)
        {
            if (!reported.Add(key)) return;
            Debug.LogWarning("[Gamesim.Uma] " + message);
        }
    }
}
