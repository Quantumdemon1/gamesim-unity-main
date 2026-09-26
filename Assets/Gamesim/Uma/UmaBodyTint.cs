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
    ///
    /// <para>Runs after <see cref="CharacterPresentation"/>, which swaps a body made behind another
    /// one into its place - a change of clothes - in its own LateUpdate: a fitting held back for that
    /// body (see <see cref="LateUpdate"/>) then happens on the frame of the swap, before it is drawn.</para>
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(100)]
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
        /// <summary>The hair and accessories wait for the body to be given a size before they are fitted.</summary>
        private bool fitPending;
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
        /// darker at all. Each such brow overlay is handed a private colour painted the brow colour,
        /// as the garments are; the hair itself, and beards, keep the hair colour.
        ///
        /// <para>A brow drawn in the shared hair colour gets a new colour rather than a copy of that
        /// one: the hair colour carries a property block (see <see cref="UmaBodyProvider.HairColour"/>)
        /// whose base colour the material draws in place of the tint, so a copy would still draw the
        /// brows in the hair colour. A brow with a private colour of its own - UMA's HD brows - gets
        /// a copy of it, repainted, so what else the recipe set there, such as its additive channel,
        /// is kept.</para>
        ///
        /// <para>UMA hands slots whose overlays match one list between them, and the trimmed beard's
        /// overlay matches the brows' - one CardWhiskers on the shared hair colour - so painting the
        /// brows' overlays painted the beard's too. A brow slot sharing its list is given a list of
        /// its own before anything on it is painted.</para>
        /// </summary>
        private void PaintBrows(UMAData data)
        {
            var slots = data?.umaRecipe?.slotDataList;
            if (slots == null) return;
            foreach (var slot in slots)
            {
                if (slot == null) continue;
                var overlays = slot.GetOverlayList();
                if (HasBrow(slot, overlays) && SharesOverlays(slots, slot, overlays))
                {
                    overlays = OwnCopy(overlays);
                    slot.SetOverlayList(overlays);
                }
                for (int i = 0; i < overlays.Count; i++)
                {
                    var overlay = overlays[i];
                    if (overlay?.colorData == null || !IsBrow(slot, overlay)) continue;
                    OverlayColorData painted;
                    if (overlay.colorData.IsASharedColor)
                    {
                        if (overlay.colorData.name != "Hair") continue;
                        painted = new OverlayColorData(3);
                    }
                    else
                    {
                        painted = overlay.colorData.Clone();
                        if (painted.channelMask == null || painted.channelMask.Length == 0) painted = new OverlayColorData(3);
                    }
                    painted.channelMask[0] = new Color(brows.r, brows.g, brows.b, painted.channelMask[0].a <= 0f ? 1f : painted.channelMask[0].a);
                    overlay.colorData = painted;
                }
            }
        }

        private static bool HasBrow(SlotData slot, List<OverlayData> overlays)
        {
            foreach (var overlay in overlays)
                if (overlay?.colorData != null && IsBrow(slot, overlay)) return true;
            return false;
        }

        private static bool SharesOverlays(SlotData[] slots, SlotData slot, List<OverlayData> overlays)
        {
            foreach (var other in slots)
                if (other != null && other != slot && ReferenceEquals(other.GetOverlayList(), overlays)) return true;
            return false;
        }

        /// <summary>
        /// A copy of the list whose overlays are copies too, still marked with where UMA merged them
        /// from. A shared colour stays the shared instance - UMA's copy would have cloned it - so a
        /// colour changed later still reaches whatever of it is not repainted.
        /// </summary>
        private static List<OverlayData> OwnCopy(List<OverlayData> overlays)
        {
            var copy = new List<OverlayData>(overlays.Count);
            foreach (var overlay in overlays)
            {
                if (overlay == null) { copy.Add(null); continue; }
                var duplicate = overlay.Duplicate();
                if (overlay.colorData != null && overlay.colorData.IsASharedColor) duplicate.colorData = overlay.colorData;
                duplicate.mergedFromSlot = overlay.mergedFromSlot;
                duplicate.mergedFromRecipe = overlay.mergedFromRecipe;
                copy.Add(duplicate);
            }
            return copy;
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
            if (rebuilding) return;
            // A change of clothes builds the new body out of sight at no size (see
            // CharacterPresentation.Dress). Its head is then a single point, a scan of it finds no
            // head to fit to, and the houseguest came out of the change bald. The fitting waits for
            // the size instead.
            if (Unscaled) fitPending = true;
            else { fitPending = false; FitGrownPieces(); }
            readyAfterFrame = Time.frameCount + 2;
        }

        private bool Unscaled => transform.lossyScale.sqrMagnitude < 1e-6f;

        /// <summary>
        /// Grows the hair and fits the accessories built in code, on the finished body: both are
        /// read off its head, so they wait for the build, and are refitted after every rebuild in
        /// case a proportion slider moved the face they sit on. Fitted to the body standing (see
        /// <see cref="StandingPose"/>), whatever it is doing when the fit lands.
        /// </summary>
        private void FitGrownPieces()
        {
            bool growing = grownHair != null, dressing = accessories != null && accessories.Count > 0;
            if (!growing && !dressing) return;
            using (StandingPose())
            {
                if (growing) ProceduralHair.Grow(gameObject, grownHair, hair, skin);
                if (dressing) ProceduralAccessories.Dress(gameObject, accessories);
            }
        }

        /// <summary>
        /// Stands the body as its controller starts it until the returned scope ends, then puts it
        /// back as it was: every layer's state and moment, and every parameter. A fit lands when a
        /// rebuild does, whatever the houseguest is doing - lying in bed after a change into their
        /// sleepwear, floating in the pool, sitting, talking, looking round at someone - and pieces
        /// fitted then were fitted to that pose: hair built for a head lying down, a nod or a turn
        /// built into the strands. Read off the body standing, they come out the same whenever the
        /// fit lands. Null, and nothing done, for a body without a humanoid controller.
        /// </summary>
        private System.IDisposable StandingPose()
        {
            var animator = GetComponentInChildren<Animator>();
            if (animator == null || !animator.isActiveAndEnabled || !animator.isHuman || animator.runtimeAnimatorController == null) return null;
            return new PoseRestorer(animator);
        }

        private sealed class PoseRestorer : System.IDisposable
        {
            private readonly Animator animator;
            private readonly int[] states;
            private readonly float[] moments, weights;
            private readonly AnimatorControllerParameter[] parameters;
            private readonly float[] values;

            public PoseRestorer(Animator animator)
            {
                this.animator = animator;
                int layers = animator.layerCount;
                states = new int[layers]; moments = new float[layers]; weights = new float[layers];
                for (int i = 0; i < layers; i++)
                {
                    // Midway into another state - lying down, getting into the water - it is the state
                    // being gone into that is put back, or the body stood up out of it.
                    var state = animator.IsInTransition(i) ? animator.GetNextAnimatorStateInfo(i) : animator.GetCurrentAnimatorStateInfo(i);
                    states[i] = state.fullPathHash; moments[i] = state.normalizedTime; weights[i] = animator.GetLayerWeight(i);
                }
                parameters = animator.parameters;
                values = new float[parameters.Length];
                for (int i = 0; i < parameters.Length; i++)
                {
                    var parameter = parameters[i];
                    if (parameter.type == AnimatorControllerParameterType.Float) values[i] = animator.GetFloat(parameter.nameHash);
                    else if (parameter.type == AnimatorControllerParameterType.Int) values[i] = animator.GetInteger(parameter.nameHash);
                    else if (parameter.type == AnimatorControllerParameterType.Bool) values[i] = animator.GetBool(parameter.nameHash) ? 1f : 0f;
                }
                animator.Rebind();
                animator.Update(0f);
            }

            public void Dispose()
            {
                if (animator == null) return;
                for (int i = 0; i < parameters.Length; i++)
                {
                    var parameter = parameters[i];
                    if (parameter.type == AnimatorControllerParameterType.Float) animator.SetFloat(parameter.nameHash, values[i]);
                    else if (parameter.type == AnimatorControllerParameterType.Int) animator.SetInteger(parameter.nameHash, (int)values[i]);
                    else if (parameter.type == AnimatorControllerParameterType.Bool) animator.SetBool(parameter.nameHash, values[i] > .5f);
                }
                for (int i = 0; i < states.Length; i++)
                {
                    if (i > 0) animator.SetLayerWeight(i, weights[i]);
                    animator.Play(states[i], i, moments[i]);
                }
                animator.Update(0f);
            }
        }

        /// <summary>
        /// Fits the pieces held back on a body built at no size, the first frame it has one. That is
        /// the frame <see cref="CharacterPresentation"/> swaps it in, sizes it and poses it as the
        /// body it replaces, earlier in the same LateUpdate pass (see the execution order above), so
        /// the pieces are read off the head as it will be drawn. The body is still marked ready
        /// while it waits, because being ready is what gets it swapped in.
        /// </summary>
        private void LateUpdate()
        {
            if (fitPending && !Unscaled) { fitPending = false; FitGrownPieces(); }
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
