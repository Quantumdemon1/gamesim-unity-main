using System;
using System.Collections.Generic;
using System.Linq;
using Gamesim.Presentation;
using Gamesim.Simulation;
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
    public sealed class UmaBodyProvider : IModularCharacterBodyProvider
    {
        private UmaAppearanceCatalog catalog;
        public UmaBodyProvider(UmaAppearanceCatalog catalog = null) { this.catalog = catalog; }
        public ICharacterAppearanceCatalog Catalog => catalog ?? (catalog = new UmaAppearanceCatalog());
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
        /// wired to the Quaternius library's idle and jog and swapped here, at runtime, for the idle and run that
        /// UMA's Locomotion controller — resolved by name, like everything else UMA — already holds.
        /// Kept in step with <c>HumanoidClipWiring.IdleStandIn</c> and <c>RunStandIn</c>.
        /// </summary>
        private const string IdleStandIn = "Idle_Loop";
        private const string RunStandIn = "Jog_Fwd_Loop";

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
            var presentation = parent == null ? null : parent.GetComponentInParent<CharacterPresentation>();
            return TryCreate(new CharacterBodyRequest(presentation == null ? appearanceId : presentation.CharacterId,
                appearanceId, null), parent, wardrobe, out body);
        }

        public bool TryCreate(in CharacterBodyRequest request, Transform parent, Color wardrobe, out CharacterBody body)
        {
            body = default;
            if (parent == null) return false;

            // Their own id first, the appearance second. The library holds a look per template, and
            // the appearance is only ever one of the handful of primitive-rig recipes — asking it
            // alone would put five faces on twenty-four people.
            var appearance = request.Appearance;
            if (appearance != null && appearance.provider != "auto" && appearance.provider != "uma") return false;
            string appearanceId = appearance?.presetId ?? request.ContestantId;
            if (!UmaCastLibrary.Resolve(appearanceId, request.FallbackId, out var preset)) return false;
            var look = new UmaCastLook
            {
                Race = string.IsNullOrEmpty(appearance?.bodyId) ? preset.Race : appearance.bodyId,
                Wardrobe = preset.Wardrobe, Skin = preset.Skin, Hair = preset.Hair,
                Brows = preset.Brows, Eyes = preset.Eyes,
                Dna = new Dictionary<string, float>(UmaCastLibrary.HouseProportions),
            };
            foreach (var entry in preset.Dna) look.Dna[entry.Key] = entry.Value;
            if (appearance != null)
            {
                foreach (var entry in appearance.dna) look.Dna[entry.id] = entry.value;
                look.Skin = AppearanceEditing.ColorValue(appearance, SkinColor, look.Skin);
                look.Hair = AppearanceEditing.ColorValue(appearance, HairColor, look.Hair);
                look.Brows = AppearanceEditing.ColorValue(appearance, BrowsColor, look.Brows);
                look.Eyes = AppearanceEditing.ColorValue(appearance, EyesColor, look.Eyes);
                var outfit = appearance.outfits.FirstOrDefault(item => item.id == appearance.activeOutfit);
                if (outfit != null) look.Wardrobe = outfit.wardrobe.Select(item => item.itemId).ToArray();
            }

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
            var buildState = root.AddComponent<CharacterBodyBuildState>();
            buildState.Revision = request.Revision;

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
            // What each saved wardrobe slot actually put on the body - a substitute, when the saved
            // garment could not be worn - so a fabric tint lands on what is drawn.
            var dressed = new Dictionary<string, UMAWardrobeRecipe>(StringComparer.Ordinal);
            var wornSet = appearance?.outfits.FirstOrDefault(item => item.id == appearance.activeOutfit)?.wardrobe;
            foreach (var recipeName in look.Wardrobe)
            {
                var installedCatalog = (UmaAppearanceCatalog)Catalog;
                var recipe = indexer.GetAsset<UMAWardrobeRecipe>(installedCatalog.ResolveRecipeName(recipeName));
                if (recipe == null || (recipe.compatibleRaces.Count > 0 && !recipe.compatibleRaces.Contains(look.Race)))
                {
                    string slot = appearance?.outfits.FirstOrDefault(item => item.id == appearance.activeOutfit)
                        ?.wardrobe.FirstOrDefault(item => item.itemId == recipeName)?.slot ?? recipe?.wardrobeSlot;
                    var replacement = installedCatalog.FindEquivalent(recipeName, slot, look.Race);
                    recipe = replacement == null ? null : indexer.GetAsset<UMAWardrobeRecipe>(installedCatalog.ResolveRecipeName(replacement.Id));
                    buildState.Substitution = recipe == null
                        ? "Some saved clothing is unavailable and temporarily hidden. Original choices are retained."
                        : "Unavailable clothing uses a compatible substitute. Original choices are retained.";
                    ReportOnce(recipeName, "wardrobe recipe '" + recipeName + "' is unavailable or incompatible; "
                        + "the saved choice is retained while a compatible fallback is shown");
                    if (recipe == null) continue;
                }
                avatar.preloadWardrobeRecipes.recipes.Add(new DynamicCharacterAvatar.WardrobeRecipeListItem(recipe));
                string wornSlot = wornSet?.FirstOrDefault(item => item.itemId == recipeName)?.slot;
                if (wornSlot != null) dressed[wornSlot] = recipe;
            }

            avatar.SetColor(SkinColor, look.Skin);
            // Hair goes in raw, with the hair shader's own colours: see HairColour.
            avatar.SetRawColor(HairColor, HairColour(look.Hair), false);
            avatar.SetColor(BrowsColor, look.Brows);
            avatar.SetColor(EyesColor, look.Eyes);
            var fabric = new Dictionary<string, Color>(StringComparer.Ordinal);
            if (appearance != null)
            {
                foreach (var color in appearance.colors)
                    if (color.id != HairColor) avatar.SetColor(color.id, new Color(color.r, color.g, color.b, color.a));
                var outfit = appearance.outfits.FirstOrDefault(item => item.id == appearance.activeOutfit);
                if (outfit != null)
                {
                    foreach (var color in outfit.colors)
                        if (!AppearanceEditing.IsFabricChannel(color.id))
                            avatar.SetColor(color.id, new Color(color.r, color.g, color.b, color.a));
                    // A fabric tint belongs to one garment: the slots its recipe builds, found here
                    // so the tint can be laid on those overlays and no others.
                    foreach (string slot in AppearanceEditing.FabricSlots)
                    {
                        if (!AppearanceEditing.TryFabric(outfit, slot, out var tint)) continue;
                        var packed = dressed.TryGetValue(slot, out var recipe) ? recipe.PackedLoad() : null;
                        if (packed?.slotsV3 == null) continue;
                        foreach (var part in packed.slotsV3)
                            if (part != null && !string.IsNullOrEmpty(part.id)) fabric[part.id] = tint;
                    }
                }
            }

            // The stylize pass, the fabric tint and the house proportions all need the assembled
            // character, so they are handed to a component that lives on the body and waits for it.
            // House proportions first, then whatever this houseguest overrides.
            root.AddComponent<UmaBodyTint>().Bind(avatar, wardrobe, look.Dna,
                preserveFabric: appearance != null, buildState: buildState, garmentTints: fabric);

            // The face. Added here rather than after the build because UMA hands the expression
            // player the race's pose set during the avatar's own Start, and only to a player that
            // is already on the object.
            root.AddComponent<UmaExpressions>();

            body = new CharacterBody(root, animator, deferred: true);
            return true;
        }

        /// <summary>
        /// A hair colour UMA 3's card hair can actually show.
        ///
        /// <para>That hair is drawn by <c>UMA3_HairShader_URP</c>, which takes its colour from three
        /// material properties - base, root and tip - and ignores the overlay's tint that every other
        /// shared colour works through. Setting "Hair" to a colour alone left those properties at the
        /// material's own pale grey over a pale texture: every houseguest's hair was white, whatever
        /// the library or the creator said. UMA's own hair presets carry the three as a property
        /// block on the shared colour; so does this, the roots a shade deeper and the tips catching
        /// a little light, as those presets do. Eyebrows and lashes share the colour and read the
        /// tint, so they follow.</para>
        /// </summary>
        internal static OverlayColorData HairColour(Color colour)
        {
            colour.a = 1f;
            var data = new OverlayColorData(3);
            data.channelMask[0] = colour;
            data.SetColorProperty("_BaseColor", colour);
            data.SetColorProperty("_RootColor", new Color(colour.r * .82f, colour.g * .82f, colour.b * .82f, 1f));
            data.SetColorProperty("_Tip_Color", Color.Lerp(colour, Color.white, .06f));
            return data;
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

            // Run first, and no "Walk" fallback. This used to ask for "Walk" and settle for "Run",
            // which sounds like a safety net and was not: UMA's Locomotion controller holds Idle,
            // Wave and Run, and no Walk at all, so the borrowed run was what every body played the
            // moment it took a step. The walk is authored now and is nobody's to lend; this clip
            // fills the RUN state, which is what it always was.
            var standing = Borrow(locomotion, "Idle");
            var running = Borrow(locomotion, "Run");
            if (standing == null || running == null || standing == running)
            {
                ReportOnce("locomotion-clips", "UMA's '" + LocomotionController + "' controller has no " +
                    "standing idle and run to lend; UMA bodies keep the authored walk and never run.");
                return cast = locomotion;
            }
            if (!Holds(houseguest, IdleStandIn) || !Holds(houseguest, RunStandIn))
            {
                ReportOnce("stand-ins", "the houseguest controller has no " + IdleStandIn + " and " +
                    RunStandIn + " to stand in for its idle and run; re-run Gamesim > U07 > Wire the " +
                    "Humanoid takes.");
                return cast = locomotion;
            }

            var overrides = new AnimatorOverrideController(houseguest) { name = "GamesimHumanoid (UMA locomotion)" };
            overrides[IdleStandIn] = standing;
            overrides[RunStandIn] = running;
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
