using System.Collections.Generic;
using Gamesim.Presentation;
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
        private bool preserveFabric;
        private CharacterBodyBuildState buildState;
        private int readyAfterFrame = -1;
        private IReadOnlyDictionary<string, Color> garmentTints;

        private Color skin = new Color(.85f, .68f, .55f), brows, hair;
        private bool hasBrows;
        private string grownHair;
        private IReadOnlyList<string> accessories;

        internal void Bind(DynamicCharacterAvatar target, Color wardrobe, IReadOnlyDictionary<string, float> dna,
            bool preserveFabric = false, CharacterBodyBuildState buildState = null,
            IReadOnlyDictionary<string, Color> garmentTints = null, Color? skin = null, Color? brows = null,
            string grownHair = null, Color? hair = null, IReadOnlyList<string> accessories = null)
        {
            avatar = target;
            fabric = wardrobe;
            proportions = dna;
            this.preserveFabric = preserveFabric;
            this.buildState = buildState;
            this.garmentTints = garmentTints;
            if (skin.HasValue) this.skin = skin.Value;
            if (brows.HasValue) { this.brows = brows.Value; hasBrows = true; }
            this.grownHair = grownHair;
            this.hair = hair ?? new Color(.2f, .14f, .1f);
            this.accessories = accessories;
            if (avatar == null) return;
            avatar.OnCharacterUpdated += OnCharacterUpdated;
            avatar.OnCharacterBegun += OnCharacterBegun;
        }

        private void OnCharacterBegun(UMAData data)
        {
            // Grown hair and accessories are fitted to the built mesh, and UMA otherwise hands that
            // mesh back with no copy the scan can read. Only bodies that wear them keep one.
            if (grownHair != null || (accessories != null && accessories.Count > 0)) data.markNotReadable = false;
            ShareHairColour(data);
            if (garmentTints != null && garmentTints.Count > 0) TintGarments(data);
            if (hasBrows) PaintBrows(data);
        }

        /// <summary>
        /// Puts every hair card on the shared hair colour. One of UMA's styles, the bun, points its
        /// card at a private white of its own rather than at "Hair", so it stayed silver whatever
        /// colour was picked. Handing it the recipe's shared instance - not a copy - means a colour
        /// changed later reaches it as it reaches every other style. Returns how many were moved.
        /// </summary>
        internal static int ShareHairColour(UMAData data)
        {
            var recipe = data?.umaRecipe;
            if (recipe?.sharedColors == null || recipe.slotDataList == null) return 0;
            OverlayColorData hair = null;
            foreach (var colour in recipe.sharedColors)
                if (colour != null && colour.name == "Hair") { hair = colour; break; }
            if (hair == null) return 0;
            int moved = 0;
            foreach (var slot in recipe.slotDataList)
            {
                if (slot == null) continue;
                var overlays = slot.GetOverlayList();
                for (int i = 0; i < overlays.Count; i++)
                {
                    var overlay = overlays[i];
                    if (overlay?.colorData == null || overlay.colorData.IsASharedColor || !IsHairCard(overlay)) continue;
                    overlay.colorData = hair;
                    moved++;
                }
            }
            return moved;
        }

        private static bool IsHairCard(OverlayData overlay)
            => overlay.overlayName != null && overlay.overlayName.StartsWith("CardHair", System.StringComparison.Ordinal);

        /// <summary>
        /// Gives the brows their own colour. Most of UMA's brow styles are drawn in the shared hair
        /// colour, not the brow colour the creator offers, so the Brows row changed nothing on them:
        /// a brunette's brows could never be darker than her hair, or a platinum blonde's any
        /// darker at all. Each such brow overlay is handed a private copy painted the brow colour, as
        /// the garments are; the hair itself, and beards, keep the hair colour.
        /// </summary>
        private void PaintBrows(UMAData data)
        {
            if (data?.umaRecipe?.slotDataList == null) return;
            foreach (var slot in data.umaRecipe.slotDataList)
            {
                if (slot == null) continue;
                var overlays = slot.GetOverlayList();
                for (int i = 0; i < overlays.Count; i++)
                {
                    var overlay = overlays[i];
                    if (overlay?.colorData == null || !IsBrow(slot, overlay)) continue;
                    if (overlay.colorData.IsASharedColor && overlay.colorData.name != "Hair") continue;
                    var painted = new OverlayColorData(3);
                    painted.channelMask[0] = new Color(brows.r, brows.g, brows.b, 1f);
                    overlay.colorData = painted;
                }
            }
        }

        private static bool IsBrow(SlotData slot, OverlayData overlay)
            => (overlay.overlayName != null && overlay.overlayName.IndexOf("brow", System.StringComparison.OrdinalIgnoreCase) >= 0)
               || (slot.slotName != null && slot.slotName.IndexOf("brow", System.StringComparison.OrdinalIgnoreCase) >= 0);

        /// <summary>
        /// Lays each tinted garment's colour on its own overlays, as UMA begins a build and before it
        /// merges the textures that carry them. The house's clothes have no shared colour of their
        /// own - their overlays each hold a private white - so a shared-colour name could never reach
        /// them; the overlay's own colour does. Only a garment's private colours are touched: a
        /// shared colour on a garment (skin showing through, say) is the character's, not the cloth's.
        /// Each colour is replaced by a copy, never edited, so no other character reading the same
        /// recipe is repainted.
        /// </summary>
        private void TintGarments(UMAData data)
        {
            if (data?.umaRecipe?.slotDataList == null || garmentTints == null) return;
            foreach (var slot in data.umaRecipe.slotDataList)
            {
                if (slot == null || !garmentTints.TryGetValue(slot.slotName, out var tint)) continue;
                var overlays = slot.GetOverlayList();
                for (int i = 0; i < overlays.Count; i++)
                {
                    var overlay = overlays[i];
                    if (overlay?.colorData == null || overlay.colorData.IsASharedColor) continue;
                    var painted = overlay.colorData.Clone();
                    if (painted.channelMask == null || painted.channelMask.Length == 0) painted = new OverlayColorData(3);
                    painted.channelMask[0] = new Color(tint.r, tint.g, tint.b, painted.channelMask[0].a <= 0f ? 1f : painted.channelMask[0].a);
                    overlay.colorData = painted;
                }
            }
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
        private bool ApplyProportions()
        {
            if (proportionsApplied || avatar == null || proportions == null || proportions.Count == 0) return false;
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
            return changed;
        }

        /// <summary>Re-tints a body whose houseguest changed palette. Safe before the build finishes.</summary>
        internal void SetFabric(Color wardrobe)
        {
            fabric = wardrobe;
            Apply();
        }

        private void OnCharacterUpdated(UMAData data)
        {
            bool rebuilding = ApplyProportions();
            UmaStylizer.Apply(gameObject, skin);
            Apply();
            if (!rebuilding) FitGrownPieces();
            if (!rebuilding) readyAfterFrame = Time.frameCount + 2;
        }

        /// <summary>
        /// Grows the hair and fits the accessories built in code, on the finished body: both are
        /// read off its head, so they wait for the build, and are refitted after every rebuild in
        /// case a proportion slider moved the face they sit on.
        /// </summary>
        private void FitGrownPieces()
        {
            if (grownHair != null) ProceduralHair.Grow(gameObject, grownHair, hair, skin);
            if (accessories != null && accessories.Count > 0) ProceduralAccessories.Dress(gameObject, accessories);
        }

        private void LateUpdate()
        {
            if (readyAfterFrame >= 0 && Time.frameCount >= readyAfterFrame && buildState != null)
                buildState.Ready = true;
        }

        private void Apply()
        {
            if (preserveFabric) return;
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
            if (avatar != null) { avatar.OnCharacterUpdated -= OnCharacterUpdated; avatar.OnCharacterBegun -= OnCharacterBegun; }
            avatar = null;
        }
    }
}
